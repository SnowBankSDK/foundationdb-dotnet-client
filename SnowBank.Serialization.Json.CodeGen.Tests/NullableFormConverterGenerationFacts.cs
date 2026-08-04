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

	/// <summary>Pins the generator half of the nullable member-converter rule: a converter declared for the member's <c>T?</c> form itself is honored (exact-then-lift, same probe order as the reflection bridge), a <c>T</c> converter keeps the lift, and a <c>T?</c> converter on a NON-nullable member stays a loud <c>CJSON0010</c></summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class NullableFormConverterGenerationFacts : SimpleTest
	{

		private const string CommonHeader = """
			namespace Probe
			{

				public sealed class NullableFormConverter : SnowBank.Data.Json.IJsonMemberConverter<int?>
				{
					public SnowBank.Data.Json.JsonValue Pack(int? instance, SnowBank.Data.Json.CrystalJsonSettings? settings = null, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver = null)
						=> instance is null ? SnowBank.Data.Json.JsonString.Return("") : SnowBank.Data.Json.JsonString.Return(instance.Value.ToString());

					public int? Unpack(SnowBank.Data.Json.JsonValue value, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver)
						=> value is SnowBank.Data.Json.JsonString { Value: var s } ? (int.TryParse(s, out var n) ? n : null) : value.ToInt32();
				}

				public sealed class LiftedConverter : SnowBank.Data.Json.IJsonMemberConverter<int>
				{
					public SnowBank.Data.Json.JsonValue Pack(int instance, SnowBank.Data.Json.CrystalJsonSettings? settings = null, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver = null)
						=> SnowBank.Data.Json.JsonString.Return(instance.ToString());

					public int Unpack(SnowBank.Data.Json.JsonValue value, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver)
						=> value.ToInt32();
				}

			""";

		private const string CommonFooter = """

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDto))]
				public static partial class ProbeConverters
				{
				}

			}
			""";

		private static (string GeneratedSource, ImmutableArray<Diagnostic> Diagnostics) RunOn(string dtoSource)
		{
			var compilation = GeneratorProbeHarness.Compile(CommonHeader + dtoSource + CommonFooter);
			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning),
				Is.Empty,
				"the probe source must compile clean on its own");
			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var d in diagnostics)
			{
				Log($"generator: [{d.Severity}] {d}");
			}
			var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			return (generated, diagnostics);
		}

		[Test]
		public void Test_NullableForm_Converter_On_Nullable_Member_Is_Honored()
		{
			var (generated, diagnostics) = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonConvertWith(typeof(NullableFormConverter))]
						public int? Count { get; set; }
					}
				""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "a T? converter on a T? member is valid, not CJSON0010");
				Assert.That(generated, Does.Contain("UnpackNullableForm("), "the read side must route through the nullable-form helper (the converter owns present values, the pipeline owns null/missing)");
				Assert.That(generated, Does.Contain("/* member-converter-nullable-form */"), "every read route takes the nullable-form branch");
				Assert.That(generated, Does.Not.Contain("/* member-converter-nullable */"), "the exact form wins over the lift");
			}
		}

		[Test]
		public void Test_Required_NullableForm_Member_Uses_The_Required_Helper()
		{
			var (generated, diagnostics) = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonConvertWith(typeof(NullableFormConverter))]
						public required int? Count { get; set; }
					}
				""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
				Assert.That(generated, Does.Contain("UnpackRequiredNullableForm("), "the presence gate stays the pipeline's, the converter still owns present values");
				Assert.That(generated, Does.Contain("/* member-converter-nullable-form-required */"), "the required variant takes the nullable-form branch too");
			}
		}

		[Test]
		public void Test_Lifted_Converter_On_Nullable_Member_Keeps_The_Lift()
		{
			var (generated, diagnostics) = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonConvertWith(typeof(LiftedConverter))]
						public int? Count { get; set; }
					}
				""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
				Assert.That(generated, Does.Contain("/* member-converter-nullable */"), "a T converter on a T? member keeps today's lifted route");
				Assert.That(generated, Does.Not.Contain("UnpackNullableForm("), "the nullable-form helper is reserved for converters declared for T? itself");
			}
		}

		[Test]
		public void Test_NullableForm_Converter_On_NonNullable_Member_Stays_Refused()
		{
			// the generator side of the loud edge: a T?-shaped converter cannot serve a non-nullable member
			var (_, diagnostics) = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonConvertWith(typeof(NullableFormConverter))]
						public int Count { get; set; }
					}
				""");
			var refusal = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0010");
			Assert.That(refusal, Is.Not.Null, "the generator must refuse the arity mismatch loudly");
			Assert.That(refusal!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
		}

	}

}
