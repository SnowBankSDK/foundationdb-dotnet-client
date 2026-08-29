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

// Simulates the Newtonsoft [JsonProperty] naming attribute WITHOUT referencing the package: both the generator
// (matched by full name) and the reflection resolver (matched by name+namespace) must recognize it the same as
// the real one.
namespace Newtonsoft.Json
{
	[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
	public sealed class JsonPropertyAttribute : System.Attribute
	{
		public JsonPropertyAttribute() { }
		public JsonPropertyAttribute(string propertyName) => this.PropertyName = propertyName;
		public string? PropertyName { get; set; }
	}

	[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
	public sealed class JsonIgnoreAttribute : System.Attribute
	{
	}
}

namespace SnowBank.Serialization.Json.CodeGen.Tests
{
	using System.Runtime.Serialization;
	using STJ = System.Text.Json.Serialization;

	#region Matrix DTOs and converters...

	public sealed record MxIgnoreDto
	{
		[STJ.JsonIgnore]
		public string? Hidden { get; init; }

		public int Kept { get; init; }
	}

	public sealed record MxIgnoreConditionsDto
	{
		[STJ.JsonIgnore(Condition = STJ.JsonIgnoreCondition.Never)]
		public int Pinned { get; init; }

		[STJ.JsonIgnore(Condition = STJ.JsonIgnoreCondition.WhenWritingNull)]
		public string? MaybeNull { get; init; }

		[STJ.JsonIgnore(Condition = STJ.JsonIgnoreCondition.WhenWritingDefault)]
		public int Count { get; init; }
	}

	// note: a [DataContract] DTO cannot appear in this matrix: registering one is rejected at build time (error
	// CJSON0014, the interim constraint until generated containers learn the DataContract contract model), so
	// there is no generated output to compare - the STJ-vs-reflection divergence for [DataContract] types stays
	// pinned by the Core.Tests DCJS parity fixtures, and the rejection by DataContractRegistrationDiagnosticFacts.
	// A [DataMember] + unconditional [STJ.JsonIgnore] pair cannot be declared here either: rejected at build
	// time on both paths (error CJSON0008 / a contract-build throw), pinned by IgnoreConflictDiagnosticFacts
	// and the Core.Tests DataContractCompat facts

	public sealed record MxStjRenameDto
	{
		[STJ.JsonPropertyName("renamed")]
		public string? Original { get; init; }
	}

	public sealed record MxSnowRenameDto
	{
		[JsonProperty("sb_name")]
		public string? Original { get; init; }
	}

	public sealed record MxNewtonsoftRenameDto
	{
		[Newtonsoft.Json.JsonProperty("nj_name")]
		public string? Original { get; init; }
	}

	public sealed record MxNewtonsoftIgnoreDto
	{
		[Newtonsoft.Json.JsonIgnore]
		public string? Hidden { get; init; }

		public int Kept { get; init; }
	}

	public sealed record MxEnumDto
	{
		public DayOfWeek Day { get; init; }
	}

	public enum MxStjTokenKind
	{
		[STJ.JsonStringEnumMemberName("C")]
		Paper = 0,

		[STJ.JsonStringEnumMemberName("E")]
		Electronic = 1,
	}

	public sealed record MxStjTokenDto
	{
		public MxStjTokenKind Kind { get; init; }
	}

	public sealed record MxEmTokenDto
	{
		public ProbeCourierKind Kind { get; init; }
	}

	public sealed record MxEnumFormatDto
	{
		[JsonProperty(EnumFormat = JsonEnumFormat.Number)]
		public DayOfWeek Day { get; init; }
	}

	/// <summary>Rung 2 of the D-21 ladder: ONE converter class valid for BOTH serializers (STJ facet + CrystalJson facets)</summary>
	public sealed class MxDualShapeBitConverter : STJ.JsonConverter<bool>, IJsonMemberConverter<bool>
	{
		// the CrystalJson facets
		public JsonValue Pack(ref CrystalJsonPackContext context, bool instance)
			=> JsonString.Return(instance ? "1" : "0");

		public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
			=> value switch
			{
				JsonString s => s.Value is "1",
				JsonBoolean b => b.ToBoolean(),
				_ => throw new JsonBindingException($"Cannot convert {value.Type} into a bit-string boolean")
			};

		// the System.Text.Json facet, producing the same output
		public override bool Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
			=> reader.TokenType switch
			{
				System.Text.Json.JsonTokenType.String => reader.GetString() is "1",
				System.Text.Json.JsonTokenType.True => true,
				System.Text.Json.JsonTokenType.False => false,
				_ => throw new System.Text.Json.JsonException()
			};

		public override void Write(System.Text.Json.Utf8JsonWriter writer, bool value, System.Text.Json.JsonSerializerOptions options)
			=> writer.WriteStringValue(value ? "1" : "0");
	}

	public sealed record MxDualShapeDto
	{
		[STJ.JsonConverter(typeof(MxDualShapeBitConverter))]
		public bool Flag { get; init; }
	}

	/// <summary>Rung 3 detector: a CrystalJson-only converter hidden behind the STJ-spelled attribute poisons the type for STJ</summary>
	public sealed record MxRung3Dto
	{
		[STJ.JsonConverter(typeof(ProbeBitStringConverter))]
		public bool Flag { get; init; }
	}

	/// <summary>Rung 1 done right: the CrystalJson-only converter carries the native attribute, which STJ never inspects</summary>
	public sealed record MxNativeDto
	{
		[JsonConvertWith(typeof(ProbeBitStringConverter))]
		public bool Flag { get; init; }
	}

	public sealed record MxBoolLiteralsDto
	{
		[JsonBooleanLiterals("0", "1")]
		public bool Flag { get; init; }
	}

	public sealed record MxCamelDto
	{
		public string? AgentName { get; init; }
	}

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(MxIgnoreDto))]
	[CrystalSerializable(typeof(MxIgnoreConditionsDto))]
	[CrystalSerializable(typeof(MxStjRenameDto))]
	[CrystalSerializable(typeof(MxSnowRenameDto))]
	[CrystalSerializable(typeof(MxNewtonsoftRenameDto))]
	[CrystalSerializable(typeof(MxNewtonsoftIgnoreDto))]
	[CrystalSerializable(typeof(MxEnumDto))]
	[CrystalSerializable(typeof(MxStjTokenDto))]
	[CrystalSerializable(typeof(MxEmTokenDto))]
	[CrystalSerializable(typeof(MxEnumFormatDto))]
	[CrystalSerializable(typeof(MxDualShapeDto))]
	[CrystalSerializable(typeof(MxRung3Dto))]
	[CrystalSerializable(typeof(MxNativeDto))]
	[CrystalSerializable(typeof(MxBoolLiteralsDto))]
	[CrystalSerializable(typeof(MxCamelDto))]
	public static partial class ParityHost
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>The STJ-parity non-regression matrix (D-23): for every attribute of the member-converter wave, pins the System.Text.Json
	/// oracle output inline (documentation-grade), asserts CrystalJson parity or the explicitly-ruled divergence side by side, and asserts
	/// the three CrystalJson routes (reflection text, DOM, source-generated) agree byte-for-byte.</summary>
	/// <remarks>
	/// <para>Escalation ladder for a type facing BOTH serializers (D-21, amended): (1) attributes coexist cleanly (each serializer
	/// ignores the other's) - one type; (2) a conflict resolvable with a dual-shape converter - one type; (3) attributes conflict and are
	/// NOT cleanly ignored - DUPLICATE the type, one DTO per serializer, never contort one type to serve both. Rung-3 pairs are exactly
	/// the rows below where the STJ oracle THROWS because of an attribute we honor: see <see cref="Test_Rung3_Pairs_Are_Exactly_The_Flagged_Ones"/>.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class StjParityMatrixFacts : SimpleTest
	{

		public sealed record ParityCase
		{
			public required string Id { get; init; }
			/// <summary>The output System.Text.Json produces for this DTO (the oracle), or <see langword="null"/> when STJ throws (see <see cref="StjThrows"/>)</summary>
			public string? StjOutput { get; init; }
			/// <summary>The output CrystalJson produces (reflection text and DOM routes); equal to <see cref="StjOutput"/> for parity rows, deliberately different for ruled divergences</summary>
			public required string CjOutput { get; init; }
			/// <summary>The output the source-generated converter produces, when it deliberately differs from <see cref="CjOutput"/> (documented path divergences); otherwise it must equal <see cref="CjOutput"/></summary>
			public string? CjGeneratedOutput { get; init; }
			/// <summary>STJ cannot serialize this DTO at all (a rung-3 pair, or an STJ limitation)</summary>
			public bool StjThrows { get; init; }
			/// <summary>Rung 3 of the D-21 ladder: our attribute poisons the type for STJ; such a type must be duplicated per serializer</summary>
			public bool Rung3 { get; init; }
			public required Func<string> RunStj { get; init; }
			public required Func<string> RunCjText { get; init; }
			public required Func<string> RunCjDom { get; init; }
			public required Func<string> RunCjGenerated { get; init; }
			public (string Output, object Expected)[] Reads { get; init; } = [ ];
			public required Func<string, object> Bind { get; init; }

			public override string ToString() => this.Id;
		}

		private static ParityCase Case<T>(
			string id,
			T dto,
			IJsonConverter<T> generated,
			string? stjOutput,
			string cjOutput,
			string? cjGeneratedOutput = null,
			CrystalJsonSettings? cjSettings = null,
			System.Text.Json.JsonSerializerOptions? stjOptions = null,
			bool stjThrows = false,
			bool rung3 = false,
			params (string Output, object Expected)[] reads)
			where T : notnull
		{
			var cj = cjSettings ?? CrystalJsonSettings.JsonCompact;
			return new()
			{
				Id = id,
				StjOutput = stjOutput,
				CjOutput = cjOutput,
				CjGeneratedOutput = cjGeneratedOutput,
				StjThrows = stjThrows,
				Rung3 = rung3,
				RunStj = () => System.Text.Json.JsonSerializer.Serialize(dto, stjOptions),
				RunCjText = () => CrystalJson.Serialize(dto, cj),
				RunCjDom = () => JsonValue.FromValue(dto, cj).ToJsonText(cj),
				RunCjGenerated = () => CrystalJson.Serialize(dto, generated, cj),
				Reads = reads,
				Bind = output => CrystalJson.Deserialize<T>(output)!,
			};
		}

		private static System.Text.Json.JsonSerializerOptions StjEnumStrings(bool camelCased = false)
			=> new() { Converters = { new STJ.JsonStringEnumConverter(camelCased ? System.Text.Json.JsonNamingPolicy.CamelCase : null) } };

		public static IEnumerable<ParityCase> Cases()
		{
			// ---- exclusion attributes ----

			yield return Case(
				id: "jsonignore-always",
				dto: new MxIgnoreDto { Hidden = "boo", Kept = 1 },
				generated: ParityHost.MxIgnoreDto.Default,
				stjOutput: """{"Kept":1}""",
				cjOutput: """{"Kept":1}""");

			// a member carrying ONLY a Newtonsoft [JsonIgnore]: the reflection path excludes it by name, and the
			// generated converter must too (STJ does not know the foreign attribute and emits the member)
			yield return Case(
				id: "newtonsoft-jsonignore",
				dto: new MxNewtonsoftIgnoreDto { Hidden = "boo", Kept = 1 },
				generated: ParityHost.MxNewtonsoftIgnoreDto.Default,
				stjOutput: """{"Hidden":"boo","Kept":1}""",
				cjOutput: """{"Kept":1}""");

			yield return Case(
				id: "jsonignore-conditions",
				dto: new MxIgnoreConditionsDto { Pinned = 0, MaybeNull = null, Count = 0 },
				generated: ParityHost.MxIgnoreConditionsDto.Default,
				stjOutput: """{"Pinned":0}""",
				cjOutput: """{"Pinned":0}""");

			// (the former "datamember-plus-jsonignore" row is gone: that pair is now rejected at build time on
			// both paths - a ruled divergence from STJ, which silently lets [JsonIgnore] win)

			// ---- DataContract interplay ----
			// STJ does not know DataContract (all public members, C# names); CrystalJson reflection honors the
			// [DataMember] opt-in and rename; and the source generator rejects a registered [DataContract] type at
			// build time (CJSON0014, the interim constraint - the former matrix divergence D1 is no longer
			// reachable), so there is no generated output to compare here: a legacy [DataContract] DTO stays on the
			// reflection path until it is modernized, and the rejection is pinned by DataContractRegistrationDiagnosticFacts.

			// ---- renames ----

			yield return Case(
				id: "stj-jsonpropertyname",
				dto: new MxStjRenameDto { Original = "X" },
				generated: ParityHost.MxStjRenameDto.Default,
				stjOutput: """{"renamed":"X"}""",
				cjOutput: """{"renamed":"X"}""");

			// coexistence rung 1: STJ ignores CrystalJson's [JsonProperty], so the same type produces two names
			yield return Case(
				id: "snowbank-jsonproperty-rename",
				dto: new MxSnowRenameDto { Original = "X" },
				generated: ParityHost.MxSnowRenameDto.Default,
				stjOutput: """{"Original":"X"}""",
				cjOutput: """{"sb_name":"X"}""");

			// a member carrying ONLY a Newtonsoft [JsonProperty] name: the reflection path honors it as a naming
			// fallback, and the generated converter must agree (STJ ignores the foreign attribute and emits the raw name)
			yield return Case(
				id: "newtonsoft-jsonproperty-rename",
				dto: new MxNewtonsoftRenameDto { Original = "X" },
				generated: ParityHost.MxNewtonsoftRenameDto.Default,
				stjOutput: """{"Original":"X"}""",
				cjOutput: """{"nj_name":"X"}""");

			// ---- enums (D-19: strings by default, deliberately diverging from STJ's numeric default) ----

			yield return Case(
				id: "enum-default",
				dto: new MxEnumDto { Day = DayOfWeek.Friday },
				generated: ParityHost.MxEnumDto.Default,
				stjOutput: """{"Day":5}""",
				cjOutput: """{"Day":"Friday"}""",
				reads: [ ("""{"Day":5}""", new MxEnumDto { Day = DayOfWeek.Friday }), ("""{"Day":"Friday"}""", new MxEnumDto { Day = DayOfWeek.Friday }), ("""{"Day":"friday"}""", new MxEnumDto { Day = DayOfWeek.Friday }), ("""{"Day":"5"}""", new MxEnumDto { Day = DayOfWeek.Friday }) ]);

			yield return Case(
				id: "enum-numbers-optin",
				dto: new MxEnumDto { Day = DayOfWeek.Friday },
				generated: ParityHost.MxEnumDto.Default,
				stjOutput: """{"Day":5}""",
				cjOutput: """{"Day":5}""",
				cjSettings: CrystalJsonSettings.JsonCompact.WithEnumAsNumbers());

			yield return Case(
				id: "enum-strings-both",
				dto: new MxEnumDto { Day = DayOfWeek.Friday },
				generated: ParityHost.MxEnumDto.Default,
				stjOutput: """{"Day":"Friday"}""",
				cjOutput: """{"Day":"Friday"}""",
				stjOptions: StjEnumStrings());

			yield return Case(
				id: "enum-strings-camel-both",
				dto: new MxEnumDto { Day = DayOfWeek.Friday },
				generated: ParityHost.MxEnumDto.Default,
				stjOutput: """{"Day":"friday"}""",
				cjOutput: """{"Day":"friday"}""",
				cjSettings: CrystalJsonSettings.JsonCompact.WithEnumAsStrings(camelCased: true),
				stjOptions: StjEnumStrings(camelCased: true));

			// enum tokens, STJ spelling: full parity when STJ opts into string enums
			yield return Case(
				id: "enum-token-stj-spelling",
				dto: new MxStjTokenDto { Kind = MxStjTokenKind.Paper },
				generated: ParityHost.MxStjTokenDto.Default,
				stjOutput: """{"Kind":"C"}""",
				cjOutput: """{"Kind":"C"}""",
				stjOptions: StjEnumStrings(),
				reads: [ ("""{"Kind":"C"}""", new MxStjTokenDto { Kind = MxStjTokenKind.Paper }), ("""{"Kind":"Paper"}""", new MxStjTokenDto { Kind = MxStjTokenKind.Paper }), ("""{"Kind":0}""", new MxStjTokenDto { Kind = MxStjTokenKind.Paper }) ]);

			// enum tokens, DataContract spelling: STJ does not read [EnumMember], CrystalJson does (D-4)
			yield return Case(
				id: "enum-token-enummember-spelling",
				dto: new MxEmTokenDto { Kind = ProbeCourierKind.Paper },
				generated: ParityHost.MxEmTokenDto.Default,
				stjOutput: """{"Kind":"Paper"}""",
				cjOutput: """{"Kind":"C"}""",
				stjOptions: StjEnumStrings(),
				reads: [ ("""{"Kind":"C"}""", new MxEmTokenDto { Kind = ProbeCourierKind.Paper }), ("""{"Kind":"Paper"}""", new MxEmTokenDto { Kind = ProbeCourierKind.Paper }) ]);

			// per-member EnumFormat: CrystalJson-only knob; here it forces numbers, which happens to restore STJ parity
			yield return Case(
				id: "enum-format-number-member",
				dto: new MxEnumFormatDto { Day = DayOfWeek.Friday },
				generated: ParityHost.MxEnumFormatDto.Default,
				stjOutput: """{"Day":5}""",
				cjOutput: """{"Day":5}""");

			// ---- converters and the D-21 ladder ----

			// rung 2: ONE dual-shape converter class, valid for both serializers, same output
			yield return Case(
				id: "converter-dual-shape",
				dto: new MxDualShapeDto { Flag = true },
				generated: ParityHost.MxDualShapeDto.Default,
				stjOutput: """{"Flag":"1"}""",
				cjOutput: """{"Flag":"1"}""",
				reads: [ ("""{"Flag":"1"}""", new MxDualShapeDto { Flag = true }), ("""{"Flag":true}""", new MxDualShapeDto { Flag = true }) ]);

			// RUNG 3: a CrystalJson-only converter behind the STJ-spelled attribute POISONS the type for STJ
			// (STJ throws building the type's metadata). Such a type must be duplicated, one DTO per serializer.
			yield return Case(
				id: "converter-cj-only-behind-stj-attribute",
				dto: new MxRung3Dto { Flag = true },
				generated: ParityHost.MxRung3Dto.Default,
				stjOutput: null,
				cjOutput: """{"Flag":"1"}""",
				stjThrows: true,
				rung3: true);

			// rung 1 done right: the native attribute, which STJ never inspects; outputs differ, nothing breaks
			yield return Case(
				id: "converter-native-attribute",
				dto: new MxNativeDto { Flag = true },
				generated: ParityHost.MxNativeDto.Default,
				stjOutput: """{"Flag":true}""",
				cjOutput: """{"Flag":"1"}""",
				reads: [ ("""{"Flag":"1"}""", new MxNativeDto { Flag = true }), ("""{"Flag":true}""", new MxNativeDto { Flag = true }) ]);

			yield return Case(
				id: "boolean-literals",
				dto: new MxBoolLiteralsDto { Flag = true },
				generated: ParityHost.MxBoolLiteralsDto.Default,
				stjOutput: """{"Flag":true}""",
				cjOutput: """{"Flag":"1"}""",
				reads: [ ("""{"Flag":"1"}""", new MxBoolLiteralsDto { Flag = true }), ("""{"Flag":true}""", new MxBoolLiteralsDto { Flag = true }) ]);

			// ---- naming policy ----

			yield return Case(
				id: "camel-cased-names",
				dto: new MxCamelDto { AgentName = "Bond" },
				generated: ParityHost.MxCamelDto.Default,
				stjOutput: """{"agentName":"Bond"}""",
				cjOutput: """{"agentName":"Bond"}""",
				cjSettings: CrystalJsonSettings.JsonCompact.CamelCased(),
				stjOptions: new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
		}

		[TestCaseSource(nameof(Cases))]
		public void Test_Stj_Oracle(ParityCase pc)
		{
			// (a) the System.Text.Json oracle: what STJ does with the same DTO, pinned inline
			if (pc.StjThrows)
			{
				Assert.That(() => pc.RunStj(), Throws.InstanceOf<Exception>(), $"[{pc.Id}] STJ is expected to REJECT this type (rung 3 when caused by an attribute we honor)");
			}
			else
			{
				Assert.That(pc.RunStj(), Is.EqualTo(pc.StjOutput), $"[{pc.Id}] the STJ oracle output drifted: update the pinned oracle AND re-examine the parity claim");
			}
		}

		[TestCaseSource(nameof(Cases))]
		public void Test_CrystalJson_Parity_Or_Ruled_Divergence(ParityCase pc)
		{
			// (b) CrystalJson output equals the STJ oracle, or the explicitly-pinned ruled divergence
			var actual = pc.RunCjText();
			Assert.That(actual, Is.EqualTo(pc.CjOutput), $"[{pc.Id}] CrystalJson output");
			if (!pc.StjThrows && pc.CjOutput != pc.StjOutput)
			{
				// divergence rows: the difference must be deliberate; this assert just keeps the side-by-side honest
				Assert.That(pc.StjOutput, Is.Not.Null.And.Not.EqualTo(pc.CjOutput), $"[{pc.Id}] this row claims a ruled divergence");
			}
		}

		[TestCaseSource(nameof(Cases))]
		public void Test_Cross_Route_Agreement(ParityCase pc)
		{
			// (c) the three CrystalJson routes agree byte-for-byte (or pin their own documented divergence)
			Assert.That(pc.RunCjDom(), Is.EqualTo(pc.CjOutput), $"[{pc.Id}] the DOM route must agree with the text route");
			Assert.That(pc.RunCjGenerated(), Is.EqualTo(pc.CjGeneratedOutput ?? pc.CjOutput), $"[{pc.Id}] the generated route must agree (or match its own documented divergence)");
		}

		[TestCaseSource(nameof(Cases))]
		public void Test_Tolerant_Reads(ParityCase pc)
		{
			// where reads are tolerant, BOTH the STJ-shaped output and ours bind to the same value
			foreach (var (output, expected) in pc.Reads)
			{
				Assert.That(pc.Bind(output), Is.EqualTo(expected), $"[{pc.Id}] reading {output}");
			}
		}

		[Test]
		public void Test_Rung3_Pairs_Are_Exactly_The_Flagged_Ones()
		{
			// the matrix doubles as the rung-3 detector: any case where an attribute we honor breaks the STJ oracle
			// is a type that must be DUPLICATED (one DTO per serializer) rather than contorted to serve both
			var rung3 = Cases().Where(c => c.Rung3).Select(c => c.Id).ToArray();
			Assert.That(rung3, Is.EqualTo(new[] { "converter-cj-only-behind-stj-attribute" }),
				"every rung-3 pair must be flagged, and every flagged case must be a genuine rung-3 pair");
			Assert.That(Cases().Where(c => c.Rung3).All(c => c.StjThrows), Is.True,
				"a rung-3 pair is by definition one where the STJ oracle breaks");
		}

	}

}
