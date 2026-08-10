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
	using System.Collections.Immutable;
	using Microsoft.CodeAnalysis;

	/// <summary>Pins <c>CJSON0008</c>: an unconditional <c>[JsonIgnore]</c> next to an include signal is a build ERROR (the dual-output DTO is not supported), while the conditional and <c>[IgnoreDataMember]</c> shapes keep their previous behavior</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class IgnoreConflictDiagnosticFacts : SimpleTest
	{

		private const string CommonHeader = """
			namespace Probe
			{

			""";

		private const string CommonFooter = """

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
				public static partial class ProbeConverters
				{
				}

			}
			""";

		private static ImmutableArray<Diagnostic> RunOn(string dtoSource, string? extraNamespaces = null)
		{
			var compilation = GeneratorProbeHarness.Compile(CommonHeader + dtoSource + CommonFooter + (extraNamespaces ?? ""));
			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning),
				Is.Empty,
				"the probe source must compile clean on its own");
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var d in diagnostics)
			{
				Log($"generator: [{d.Severity}] {d}");
			}
			return diagnostics;
		}

		private static void AssertRefusedLoudly(ImmutableArray<Diagnostic> diagnostics, string includeSignal)
		{
			var conflict = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0008");
			Assert.That(conflict, Is.Not.Null, "the generator must report CJSON0008 for the conflicting member");
			Assert.That(conflict!.Severity, Is.EqualTo(DiagnosticSeverity.Error), "an unconditional [JsonIgnore] next to an include signal is an ERROR: a mid-port project carries thousands of interim warnings, and a warning drowns where an error gets read");
			var message = conflict.GetMessage();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(message, Does.Contain(includeSignal), "the message names the include signal");
				Assert.That(message, Does.Contain("one DTO per serializer"), "the primary remedy is the split (the same greppable phrase as the conflicting-wire-names family)");
				Assert.That(message, Does.Not.Contain("Condition"), "a Condition suggestion would resolve the error while shipping the member onto the second wire");
			}
		}

		[Test]
		public void Test_DataMember_Plus_Unconditional_JsonIgnore_Is_A_Build_Error()
		{
			// the probe deliberately has NO type-level [DataContract]: on a [DataContract] type the whole
			// enrolment is refused first (CJSON0014, pinned by DataContractRefusalDiagnosticFacts), so the
			// member-level conflict is only reachable on a plain DTO carrying a stray [DataMember]
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[System.Runtime.Serialization.DataMember]
						[System.Text.Json.Serialization.JsonIgnore]
						public string? Both { get; set; }

						public int Plain { get; set; }
					}
				""");
			AssertRefusedLoudly(diagnostics, "DataMember");
		}

		[Test]
		public void Test_JsonInclude_Plus_Unconditional_JsonIgnore_Is_A_Build_Error()
		{
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[System.Text.Json.Serialization.JsonInclude]
						[System.Text.Json.Serialization.JsonIgnore]
						public string? Both { get; set; }
					}
				""");
			AssertRefusedLoudly(diagnostics, "JsonInclude");
		}

		[Test]
		public void Test_JsonProperty_Plus_Unconditional_JsonIgnore_Is_A_Build_Error()
		{
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonProperty("actif")]
						[System.Text.Json.Serialization.JsonIgnore]
						public string? Both { get; set; }
					}
				""");
			AssertRefusedLoudly(diagnostics, "JsonProperty");
		}

		[Test]
		public void Test_Newtonsoft_Style_JsonIgnore_Is_The_Same_Conflict()
		{
			// the reflection path matches [JsonIgnore] by name (any namespace), so the generator must flag the
			// Newtonsoft-style spelling too or the two paths would disagree on the same member
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[System.Runtime.Serialization.DataMember]
						[Newtonsoft.Json.JsonIgnore]
						public string? Both { get; set; }
					}
				""");
			AssertRefusedLoudly(diagnostics, "DataMember");
		}

		[Test]
		public void Test_Conditional_JsonIgnore_Is_Not_A_Conflict()
		{
			// no type-level [DataContract] here: it would trip the CJSON0014 enrolment refusal and the member
			// would never be parsed, making this assertion vacuous
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[System.Runtime.Serialization.DataMember(EmitDefaultValue = false)]
						[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
						public int Count { get; set; }
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0008"), Is.Empty, "a Condition is a write rule, not an exclusion: no conflict");
		}

		[Test]
		public void Test_IgnoreDataMember_Pair_Stays_A_Warning()
		{
			// the sibling shape ([IgnoreDataMember] next to an include signal) is NOT part of the ruled escalation:
			// it keeps the historical warning (on a [DataContract] type, DCJS's own precedence lets [DataMember] govern)
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[System.Runtime.Serialization.DataMember]
						[System.Runtime.Serialization.IgnoreDataMember]
						public string? Both { get; set; }
					}
				""");
			var conflict = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0008");
			Assert.That(conflict, Is.Not.Null, "the contradictory intent is still surfaced");
			Assert.That(conflict!.Severity, Is.EqualTo(DiagnosticSeverity.Warning), "but it is not part of the ruled error escalation");
		}

	}

}
