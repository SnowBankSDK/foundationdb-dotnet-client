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

	// note: the Newtonsoft.Json.JsonPropertyAttribute the probes reference is defined once in the test assembly
	// (see StjParityMatrixFacts.cs) and imported here, so the probe source must NOT redefine it (that would be CS0436)

	/// <summary>Pins <c>CJSON0011</c> for the JSON naming attributes: a member carrying several naming attributes
	/// (CrystalJson <c>[JsonProperty]</c>, STJ <c>[JsonPropertyName]</c>, Newtonsoft <c>[JsonProperty]</c>) with
	/// DIFFERENT names is a dual-output DTO and is rejected; the same name across them is not a conflict.</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class NamingConflictDiagnosticFacts : SimpleTest
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

		private static ImmutableArray<Diagnostic> RunOn(string dtoSource)
		{
			var compilation = GeneratorProbeHarness.Compile(CommonHeader + dtoSource + CommonFooter);
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

		private static void AssertNamingConflict(ImmutableArray<Diagnostic> diagnostics)
		{
			var conflict = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0011");
			Assert.That(conflict, Is.Not.Null, "the generator must report CJSON0011 for the member declaring two different output names");
			Assert.That(conflict!.Severity, Is.EqualTo(DiagnosticSeverity.Error), "two different output names on one member is an error, not a warning");
			Assert.That(conflict.GetMessage(), Does.Contain("one DTO per serializer"), "the remedy is the split (the same greppable phrase as the rest of the conflicting-name family)");
		}

		[Test]
		public void Test_Native_And_Newtonsoft_Disagreeing_Names_Is_A_Build_Error()
		{
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonProperty("A")]
						[Newtonsoft.Json.JsonProperty("B")]
						public string? Code { get; set; }
					}
				""");
			AssertNamingConflict(diagnostics);
		}

		[Test]
		public void Test_Stj_And_Newtonsoft_Disagreeing_Names_Is_A_Build_Error()
		{
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[System.Text.Json.Serialization.JsonPropertyName("A")]
						[Newtonsoft.Json.JsonProperty("B")]
						public string? Code { get; set; }
					}
				""");
			AssertNamingConflict(diagnostics);
		}

		[Test]
		public void Test_Native_And_Newtonsoft_Same_Name_Is_Not_A_Conflict()
		{
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonProperty("CODE")]
						[Newtonsoft.Json.JsonProperty("CODE")]
						public string? Code { get; set; }
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0011"), Is.Empty, "the same name across attributes is not a conflict");
		}

		[Test]
		public void Test_Newtonsoft_Only_Name_Is_Not_A_Conflict()
		{
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[Newtonsoft.Json.JsonProperty("CODE_UNIVERSE")]
						public string? Code { get; set; }
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0011"), Is.Empty, "a single naming attribute is never a conflict");
		}
	}
}
