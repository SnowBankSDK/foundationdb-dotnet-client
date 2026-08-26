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

	/// <summary>The DataContractJsonSerializer parity matrix: for every DCJS attribute/construct of the legacy migration, pins the output
	/// the REAL legacy serializer produces (live, in-process; the net472 leg runs the actual .NET Framework DCJS) next to CrystalJson's
	/// output, and asserts SEMANTIC compatibility in both directions rather than byte equality.</summary>
	/// <remarks>
	/// <para>The bar (owner-ruled): "similar enough that it will work with well-behaved clients". Field names and membership are
	/// respected, ignored members stay ignored, and the two read directions are the mechanical proof: CrystalJson binds the DCJS output
	/// to the same value (stored data, rolling upgrades), and DCJS binds CrystalJson's compat-mode output (frozen legacy clients). Byte
	/// differences that a well-behaved parser absorbs (ISO vs <c>\/Date()\/</c>, omitted vs explicit nulls, key order, <c>\/</c>
	/// escaping) are pinned side by side as documentation, not asserted away.</para>
	/// <para>The <c>CjLegacyOutput</c> column is the migration recipe: the exact settings that produce an output a frozen DCJS client reads.</para>
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

		/// <summary>POCO (no [DataContract]) with one member per legacy value format; members are declared in alphabetical order because DCJS orders POCO members alphabetically</summary>
		public sealed class DxPocoFormatsDto
		{
			public Dictionary<string, int>? Counts { get; set; }

			public TimeSpan Elapsed { get; set; }

			public DayOfWeek Kind { get; set; }

			public DateTime When { get; set; }
		}

		/// <summary>Single DateTime member, for the per-Kind oracle probes (the output depends on the machine timezone, so it is pinned against the live oracle, not inline)</summary>
		public sealed class DxDateKindDto
		{
			public DateTime When { get; set; }
		}

		/// <summary>The four lifecycle callbacks, in the modern signatures: parameterless everywhere, and the deserialize pair may take the document</summary>
		[DataContract]
		public sealed class DxCallbackDto
		{
			[DataMember(Name = "id")]
			public string? Id { get; set; }

			[IgnoreDataMember]
			public List<string> Trace { get; } = [];

			[IgnoreDataMember]
			public string? SawDocument { get; set; }

			[OnSerializing]
			private void BeforeWrite() => this.Trace.Add("OnSerializing");

			[OnSerialized]
			private void AfterWrite() => this.Trace.Add("OnSerialized");

			[OnDeserializing]
			private void BeforeRead() => this.Trace.Add("OnDeserializing");

			[OnDeserialized]
			private void AfterRead(JsonObject document)
			{
				this.Trace.Add("OnDeserialized");
				this.SawDocument = document.ToJsonText(CrystalJsonSettings.JsonCompact);
			}
		}

		/// <summary>The legacy DCJS callback signature, which is refused on both paths</summary>
		[DataContract]
		public sealed class DxLegacyCallbackDto
		{
			[DataMember(Name = "id")]
			public string? Id { get; set; }

			[OnDeserialized]
			private void AfterRead(StreamingContext context) { }
		}

		/// <summary>Standalone KeyValuePair members: DCJS serializes the pair's own contract, which uses LOWERCASE "key"/"value" (unlike the dictionary pair-array form, which uses "Key"/"Value")</summary>
		[DataContract]
		public sealed record DxKvpDto
		{
			[DataMember(Name = "pair")]
			public KeyValuePair<string, int> Pair { get; set; }

			[DataMember(Name = "pairs")]
			public List<KeyValuePair<string, int>>? Pairs { get; set; }
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

		/// <summary>Asserts a case: the live DCJS oracle output, the CrystalJson output on both routes, and the two read directions</summary>
		private static void Check<T>(
			T dto,
			string dcjsOutput,
			string cjOutput,
			CrystalJsonSettings? cjSettings = null,
			string? cjLegacyOutput = null,
			CrystalJsonSettings? cjLegacySettings = null,
			Action<T>? verifyCjRead = null,
			Action<T>? verifyDcjsRead = null)
			where T : notnull
		{
			var cj = cjSettings ?? Compact;

			// the ORACLE: what the real DataContractJsonSerializer produces (documentation-grade, pinned inline by the caller)
			Assert.That(DcjsSerialize(dto), Is.EqualTo(dcjsOutput), "the DCJS oracle output drifted");

			// CrystalJson's output, byte-identical across its own routes
			Assert.That(CrystalJson.Serialize(dto, cj), Is.EqualTo(cjOutput), "CrystalJson text route");
			Assert.That(JsonValue.FromValue(dto, cj).ToJsonText(cj), Is.EqualTo(cjOutput), "CrystalJson DOM route must agree");

			// the legacy-compat output, when the default format is not what a frozen DCJS client can read
			if (cjLegacyOutput != null)
			{
				Assert.That(CrystalJson.Serialize(dto, cjLegacySettings ?? Compact), Is.EqualTo(cjLegacyOutput), "CrystalJson legacy-compat output (the migration recipe)");
			}

			// read direction A: CrystalJson binds the output the legacy serializer produced (stored data, rolling upgrades)
			var fromDcjs = CrystalJson.Deserialize<T>(dcjsOutput)!;
			if (verifyCjRead != null) verifyCjRead(fromDcjs);
			else Assert.That(fromDcjs, Is.EqualTo(dto), "CrystalJson must bind the DCJS output to the same value");

			// read direction B: the legacy serializer binds CrystalJson's (compat) output (frozen legacy clients)
			var fromCj = DcjsDeserialize<T>(cjLegacyOutput ?? cjOutput);
			if (verifyDcjsRead != null) verifyDcjsRead(fromCj);
			else Assert.That(fromCj, Is.EqualTo(dto), "the legacy DCJS client must bind CrystalJson's output to the same value");
		}

		[Test]
		public void Test_DataMember_OptIn_And_Rename()
		{
			// [DataContract] opt-in + [DataMember(Name=...)]: full semantic parity
			Check(
				new DxRenameDto { Id = "X", NotAMember = "n" },
				dcjsOutput: """{"renamed_id":"X"}""",
				cjOutput: """{"renamed_id":"X"}""",
				// NotAMember cannot round-trip through the output on either serializer: compare only the contract members
				verifyCjRead: v => Assert.That(v.Id, Is.EqualTo("X")),
				verifyDcjsRead: v => Assert.That(v.Id, Is.EqualTo("X")));
		}

		[Test]
		public void Test_IgnoreDataMember_Is_Honored()
		{
			// [IgnoreDataMember] is DCJS's opt-out on non-[DataContract] types; CrystalJson must exclude the member as well
			Check(
				new DxIgnoredDto { Kept = 1, Secret = "s" },
				dcjsOutput: """{"Kept":1}""",
				cjOutput: """{"Kept":1}""",
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
				dcjsOutput: """{"Zulu":"z","Alpha":"a"}""",
				cjOutput: """{"Alpha":"a","Zulu":"z"}""");
		}

		[Test]
		public void Test_EmitDefaultValue_False()
		{
			// DCJS omits the default; CrystalJson emits it (harmless extra for a well-behaved reader).
			// The modern rewrite is [JsonIgnore(WhenWritingDefault)], per the wave-2 table.
			Check(
				new DxDefaultsDto { Count = 0, Quantity = 0 },
				dcjsOutput: """{"Count":0}""",
				cjOutput: """{"Count":0,"Quantity":0}""");
		}

		[Test]
		public void Test_Null_Members()
		{
			// DCJS emits explicit nulls; CrystalJson omits them by default. Both readers treat missing and null alike.
			// WithNullMembers() restores byte-level emission for legacy endpoints that want it.
			Check(
				new DxNullsDto { Name = null, Value = 1 },
				dcjsOutput: """{"Name":null,"Value":1}""",
				cjOutput: """{"Value":1}""",
				cjLegacyOutput: """{"Name":null,"Value":1}""",
				cjLegacySettings: Compact.WithNullMembers());
		}

		[Test]
		public void Test_IsRequired_Hazard()
		{
			// the one place where CrystalJson's omit-nulls default can BREAK a legacy client: a [DataMember(IsRequired=true)]
			// member that is null gets omitted, and the legacy DCJS reader THROWS on the missing required member.
			// Recipe: serialize such endpoints with WithNullMembers() (or guarantee the value is present).
			var dto = new DxRequiredDto { Id = null };
			var cjOutput = CrystalJson.Serialize(dto, Compact);
			Assert.That(cjOutput, Is.EqualTo("{}"), "CrystalJson omits the null member by default");
			Assert.That(() => DcjsDeserialize<DxRequiredDto>(cjOutput), Throws.InstanceOf<SerializationException>(),
				"the legacy reader requires the member: this is the documented hazard");

			// the recipe restores compatibility
			var legacyOutput = CrystalJson.Serialize(dto, Compact.WithNullMembers());
			Assert.That(legacyOutput, Is.EqualTo("""{"Id":null}"""));
			Assert.That(DcjsDeserialize<DxRequiredDto>(legacyOutput).Id, Is.Null);
		}

		[Test]
		public void Test_DateTime_Formats()
		{
			// DCJS: the \/Date(ms)\/ epoch form; CrystalJson: ISO 8601 by default, tolerant read of both,
			// WithMicrosoftDates() for frozen legacy readers (which reject ISO).
			var when = new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc); // epoch 1234567890000
			Check(
				new DxDatesDto { When = when },
				dcjsOutput: """{"When":"\/Date(1234567890000)\/"}""",
				cjOutput: """{"When":"2009-02-13T23:31:30Z"}""",
				cjLegacyOutput: """{"When":"\/Date(1234567890000)\/"}""",
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
				dcjsOutput: """{"Kind":1}""",
				cjOutput: """{"Kind":"E"}""",
				cjLegacyOutput: """{"Kind":1}""",
				cjLegacySettings: Compact.WithEnumAsNumbers());
		}

		[Test]
		public void Test_Dictionaries()
		{
			// DCJS: an array of {"Key":..,"Value":..} pairs; CrystalJson: a JSON object map, with default-on read
			// tolerance for the legacy shape, and WithDictionariesAsPairArrays() for frozen legacy readers.
			Check(
				new DxDictDto { Counts = new() { ["a"] = 1, ["b"] = 2 } },
				dcjsOutput: """{"Counts":[{"Key":"a","Value":1},{"Key":"b","Value":2}]}""",
				cjOutput: """{"Counts":{"a":1,"b":2}}""",
				cjLegacyOutput: """{"Counts":[{"Key":"a","Value":1},{"Key":"b","Value":2}]}""",
				cjLegacySettings: Compact.WithDictionariesAsPairArrays());
		}

		[Test]
		public void Test_Collections()
		{
			// plain arrays/lists: full parity
			Check(
				new DxListDto { Tags = [ "x", "y" ] },
				dcjsOutput: """{"Tags":["x","y"]}""",
				cjOutput: """{"Tags":["x","y"]}""");
		}

		[Test]
		public void Test_Slash_Escaping()
		{
			// DCJS escapes '/' as '\/' (in every string, not just dates); CrystalJson does not.
			// Both are valid JSON encodings of the same string: semantic parity despite the byte difference.
			Check(
				new DxSlashDto { Path = "a/b" },
				dcjsOutput: """{"Path":"a\/b"}""",
				cjOutput: """{"Path":"a/b"}""");
		}

		[Test]
		public void Test_Poco_Gets_The_Same_Value_Formats_As_DataContract_Types()
		{
			// DCJS's value-format layer is membership-model-independent: a POCO without [DataContract] still gets
			// the Microsoft date, the ISO 8601 duration, the pair-array dictionary and the numeric enum. Proven
			// against the live oracle rather than believed, because the DataContractCompat profile builds on it:
			// a DataContractCompat endpoint produces what DCJS would have produced for the same type, POCO or
			// [DataContract] alike.
			var dto = new DxPocoFormatsDto
			{
				Counts = new() { ["a"] = 1 },
				Elapsed = new TimeSpan(1, 2, 3, 4, 5),
				Kind = DayOfWeek.Friday,
				When = new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc),
			};

			var dcjs = DcjsSerialize(dto);
			Log($"dcj: {dcjs}");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dcjs, Does.Contain(@"\/Date(1234567890000)\/"), "DCJS emits the Microsoft date on a POCO too");
				Assert.That(dcjs, Does.Contain("P1DT2H3M4.005S"), "DCJS emits the ISO 8601 duration on a POCO too");
				Assert.That(dcjs, Does.Contain("\"Key\":\"a\""), "DCJS emits the pair-array dictionary on a POCO too");
				Assert.That(dcjs, Does.Contain("\"Kind\":5"), "DCJS emits the numeric enum on a POCO too");
			}

			var cj = CrystalJson.Serialize(dto, CrystalJsonSettings.DataContractCompat.Compacted());
			Log($"cj : {cj}");
			// NOTE, before you reuse this assertion elsewhere: BYTE equality holds here only because this DTO's
			// declaration order happens to match DCJS's, which sorts members alphabetically by output name (and, when
			// Order= is present, unordered members first then by Order with alphabetical ties). Both of our paths
			// emit declaration order. Reorder these members and this assertion fails without anything being wrong,
			// so a new case with a different declaration order must compare membership and values, not bytes.
			Assert.That(cj, Is.EqualTo(dcjs), "a DataContractCompat endpoint produces what DCJS would have produced for the same type");
		}

		[Test]
		public void Test_Standalone_KeyValuePair_Binds_The_Dcjs_Output()
		{
			// DCJS serializes a KeyValuePair member through the pair's own generic contract: lowercase "key"/"value"
			var dto = new DxKvpDto { Pair = new("k", 7), Pairs = [ new("a", 1), new("b", 2) ] };
			var oracle = DcjsSerialize(dto);
			Assert.That(oracle, Is.EqualTo("""{"pair":{"key":"k","value":7},"pairs":[{"key":"a","value":1},{"key":"b","value":2}]}"""), "the DCJS oracle output drifted");

			// the lowercase legacy output must BIND, not silently produce default-filled pairs
			var back = CrystalJson.Deserialize<DxKvpDto>(oracle)!;
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Pair.Key, Is.EqualTo("k"), "the lowercase key field binds");
				Assert.That(back.Pair.Value, Is.EqualTo(7), "the lowercase value field binds");
				Assert.That(back.Pairs, Is.EqualTo(new List<KeyValuePair<string, int>> { new("a", 1), new("b", 2) }), "list elements bind through the same shape");
			}

			// the uppercase (STJ-shaped) object and our own 2-element array form keep binding
			Assert.That(CrystalJson.Deserialize<DxKvpDto>("""{"pair":{"Key":"k","Value":7}}""")!.Pair, Is.EqualTo(new KeyValuePair<string, int>("k", 7)));
			Assert.That(CrystalJson.Deserialize<DxKvpDto>("""{"pair":["k",7]}""")!.Pair, Is.EqualTo(new KeyValuePair<string, int>("k", 7)));

			// an object that is not a KVP shape at all refuses loudly, same posture as the pair-array strictness
			Assert.That(() => CrystalJson.Deserialize<DxKvpDto>("""{"pair":{"foo":1}}"""), Throws.InstanceOf<JsonBindingException>(), "an unrecognizable object refuses instead of defaulting silently");

			// write side is UNCHANGED in this fix (held for the sample numbers): our output stays the 2-element array
			var ourOutput = CrystalJson.Parse(CrystalJson.Serialize(dto, Compact)).AsObject();
			Assert.That(ourOutput["pair"], Is.InstanceOf<JsonArray>().With.Count.EqualTo(2), "the write side keeps the documented 2-element-array form");
		}

		[Test]
		public void Test_Date_Writes_Match_The_Oracle_For_Every_DateTimeKind()
		{
			// DCJS appends the machine's UTC offset for non-Utc kinds ("\/Date(ms+HHMM)\/"), so those bytes depend
			// on the machine timezone: the pin is EQUALITY WITH THE LIVE ORACLE, never an inline literal. This
			// closes the byte-fidelity claim for the one axis the fixed-literal pins cannot cover.
			var compat = CrystalJsonSettings.DataContractCompat.Compacted();

			// Utc: no offset suffix, on either serializer
			var utc = new DxDateKindDto { When = new DateTime(2024, 9, 20, 12, 34, 56, DateTimeKind.Utc) };
			var oracleUtc = DcjsSerialize(utc);
			Log($"utc  : {oracleUtc}");
			Assert.That(oracleUtc, Does.Not.Match(@"[+-]\d{4}"), "probe sanity: DCJS writes no offset for a Utc value");
			Assert.That(CrystalJson.Serialize(utc, compat), Is.EqualTo(oracleUtc), "byte fidelity, Utc kind");

			// Local: DCJS converts to UTC epoch ms and appends the offset at that date
			var local = new DxDateKindDto { When = new DateTime(2024, 9, 20, 12, 34, 56, DateTimeKind.Local) };
			var oracleLocal = DcjsSerialize(local);
			Log($"local: {oracleLocal}");
			Assert.That(oracleLocal, Does.Match(@"[+-]\d{4}\)"), "probe sanity: DCJS did emit an offset suffix for a Local value");
			Assert.That(CrystalJson.Serialize(local, compat), Is.EqualTo(oracleLocal), "byte fidelity, Local kind");
			Assert.That(JsonValue.FromValue(local, compat).ToJsonText(compat), Is.EqualTo(oracleLocal), "the DOM route agrees on the Local output");

			// Unspecified: whatever the oracle does (offset or not) is the contract; pin agreement, log the shape
			var unspecified = new DxDateKindDto { When = new DateTime(2024, 9, 20, 12, 34, 56, DateTimeKind.Unspecified) };
			var oracleUnspecified = DcjsSerialize(unspecified);
			Log($"unsp : {oracleUnspecified}");
			Assert.That(CrystalJson.Serialize(unspecified, compat), Is.EqualTo(oracleUnspecified), "byte fidelity, Unspecified kind");

			// and a winter date, so a DST-dependent offset bug cannot hide behind the season of the test run
			var winter = new DxDateKindDto { When = new DateTime(2024, 1, 15, 8, 0, 0, DateTimeKind.Local) };
			Assert.That(CrystalJson.Serialize(winter, compat), Is.EqualTo(DcjsSerialize(winter)), "byte fidelity, Local kind, non-DST date");
		}

		[Test]
		public void Test_NonPublic_DataMember()
		{
			// DCJS serializes a private [DataMember], and so does CrystalJson (hybrid rule: the DataContract model
			// is accessibility-blind, and the attribute pair is already the explicit declaration of intent)
			var dto = new DxPrivateDto { Kept = 1 };
			dto.SetSecret("s");
			Assert.That(DcjsSerialize(dto), Is.EqualTo("""{"Kept":1,"Secret":"s"}"""), "the legacy serializer includes the private member");
			Assert.That(CrystalJson.Serialize(dto, Compact), Is.EqualTo("""{"Kept":1,"Secret":"s"}"""), "hybrid rule: a non-public [DataMember] on a [DataContract] type serializes automatically, matching DCJS");
			var back = CrystalJson.Deserialize<DxPrivateDto>("""{"Kept":1,"Secret":"s"}""")!;
			Assert.That(back.GetSecret(), Is.EqualTo("s"), "and the private member binds on read");

			// the [JsonInclude] interim opt-in stays legal; on a [DataContract] type it is now simply redundant
			var included = new DxPrivateIncludedDto { Kept = 1 };
			included.SetSecret("s");
			Assert.That(CrystalJson.Serialize(included, Compact), Is.EqualTo("""{"Kept":1,"Secret":"s"}"""), "[DataMember] + [JsonInclude] keeps the same output");
		}

		[Test]
		public void Test_Lifecycle_Callbacks_Fire_On_Both_Write_Routes_And_On_Read()
		{
			// the four DCJS callbacks are honoured, so a ported estate keeps the behaviour it had, but only in the
			// modern signatures: the legacy StreamingContext parameter is refused (see the next test)
			var dto = new DxCallbackDto { Id = "X" };

			Assert.That(CrystalJson.Serialize(dto, Compact), Is.EqualTo("""{"id":"X"}"""));
			Assert.That(dto.Trace, Is.EqualTo(new[] { "OnSerializing", "OnSerialized" }), "text route");

			dto.Trace.Clear();
			Assert.That(JsonValue.FromValue(dto, Compact).ToJsonText(Compact), Is.EqualTo("""{"id":"X"}"""));
			Assert.That(dto.Trace, Is.EqualTo(new[] { "OnSerializing", "OnSerialized" }), "DOM route must fire them too, or the two routes disagree");

			var back = CrystalJson.Deserialize<DxCallbackDto>("""{"id":"X"}""")!;
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Trace, Is.EqualTo(new[] { "OnDeserializing", "OnDeserialized" }), "read fires both, in order");
				Assert.That(back.Id, Is.EqualTo("X"), "OnDeserializing runs BEFORE members are populated, so it cannot clobber them");
				// the capability the legacy serializer never had: the callback can see the document it was bound from
				Assert.That(back.SawDocument, Is.EqualTo("""{"id":"X"}"""), "a callback declared with a JsonObject parameter receives the incoming document");
			}
		}

		[Test]
		public void Test_Legacy_StreamingContext_Callback_Is_Refused_With_The_Shared_Message()
		{
			// DCJS REQUIRES this parameter, so refusing it is a deliberate breaking change: the callsite is a
			// search-and-replace, and the type stops being serializable by DCJS once converted. Refused at
			// contract-build time, once per type, never per invocation.
			var ex = Assert.Throws<JsonBindingException>(() => CrystalJson.Serialize(new DxLegacyCallbackDto { Id = "X" }, Compact));
			Assert.That(ex!.Message, Is.EqualTo(string.Format(CrystalJson.Errors.CallbackStreamingContextNotSupported, $"{typeof(DxLegacyCallbackDto).FullName}.AfterRead")));
			Assert.That(ex.Message, Does.StartWith("Remove the StreamingContext parameter"), "the message leads with the fix");

			// and the same refusal on the read side, from the same contract build
			Assert.That(() => CrystalJson.Deserialize<DxLegacyCallbackDto>("""{"id":"X"}"""), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_CollectionDataContract_Naming_Is_Absent_From_Json()
		{
			// [CollectionDataContract]'s Name / ItemName / KeyName / ValueName shape the XML output only. In JSON the
			// attribute carries exactly one visible meaning, "this type is a collection", which the collection binders
			// already provide for subclasses of List<T> / Collection<T> / Dictionary<K,V>. Pinned against the live
			// oracle rather than reasoned about, because the four names look load-bearing and a migration that
			// believes they are will go hunting for machinery that does not need to exist.
			var dto = new Acme.Zoo.Cases.CollectionDataContractNaming.NamedCollectionDto
			{
				Bag = [ "x", "y" ],
				Labels = new() { ["c1"] = "Label one" },
			};

			Check(
				dto,
				dcjsOutput: """{"bag":["x","y"],"labels":[{"Key":"c1","Value":"Label one"}]}""",
				cjOutput: """{"bag":["x","y"],"labels":{"c1":"Label one"}}""",
				cjLegacyOutput: """{"bag":["x","y"],"labels":[{"Key":"c1","Value":"Label one"}]}""",
				cjLegacySettings: CrystalJsonSettings.DataContractCompat.Compacted(),
				verifyCjRead: VerifyBoundShape,
				verifyDcjsRead: VerifyBoundShape);

			// the knobs that look most load-bearing: a pair's members are the literal "Key"/"Value" in the output,
			// never the configured KeyName="code" / ValueName="label"
			var pair = CrystalJson.Parse(CrystalJson.Serialize(dto, CrystalJsonSettings.DataContractCompat.Compacted())).AsObject().GetArray("labels")[0].AsObject();
			Assert.That(pair.Keys, Is.EquivalentTo(new[] { "Key", "Value" }), "KeyName/ValueName have no JSON existence");

			static void VerifyBoundShape(Acme.Zoo.Cases.CollectionDataContractNaming.NamedCollectionDto v)
			{
				// the DERIVED types must come back, not the List<T> / Dictionary<K,V> they extend
				Assert.That(v.Bag, Is.InstanceOf<Acme.Zoo.Cases.CollectionDataContractNaming.ItemBag>().And.EqualTo(new[] { "x", "y" }));
				Assert.That(v.Labels, Is.InstanceOf<Acme.Zoo.Cases.CollectionDataContractNaming.LabelMap>().And.EqualTo(new Dictionary<string, string> { ["c1"] = "Label one" }));
			}
		}

	}

}
