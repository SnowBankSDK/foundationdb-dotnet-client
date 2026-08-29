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
	using Microsoft.CodeAnalysis;

	#region Probe types...

	public sealed record ProfiledOrder
	{
		public DayOfWeek Kind { get; set; }

		public DateTime When { get; set; }

		public TimeSpan Elapsed { get; set; }

		public Dictionary<string, int>? Counts { get; set; }

		public string? MaybeNull { get; set; }
	}

	/// <summary>The legacy-output container: serves an unchanged DCJS reader, one endpoint at a time (the not-yet-ported services of a WCF portage)</summary>
	[CrystalJsonConverter(CrystalJsonSerializerDefaults.DataContractCompat)]
	[CrystalSerializable(typeof(ProfiledOrder))]
	public static partial class LegacyConverters
	{
		// generated code goes here!
	}

	/// <summary>The modern container over the SAME types: the target state once a service is ported (delete the legacy container when the portage completes)</summary>
	[CrystalJsonConverter]
	[CrystalSerializable(typeof(ProfiledOrder))]
	public static partial class ModernConverters
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>Pins the container-level <c>DataContractCompat</c> profile: the baked default format, the pass-through of explicit settings, the loud incompatibility failure, and the dual-container pattern</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class ContainerProfileFacts : SimpleTest
	{

		private static ProfiledOrder MakeSample() => new()
		{
			Kind = DayOfWeek.Friday,
			When = new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc),
			Elapsed = new TimeSpan(1, 2, 3, 4, 5),
			Counts = new() { ["a"] = 1 },
			MaybeNull = null,
		};

		[Test]
		public void Test_Profiled_Container_Emits_The_Legacy_Output_By_Default()
		{
			// no settings passed: the container's baked profile IS the default format
			var json = LegacyConverters.ProfiledOrder.ToJsonText(MakeSample());
			Log(json);
			var obj = CrystalJson.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Kind"], Is.InstanceOf<JsonNumber>(), "numeric enum: the baked profile applies without any per-call settings");
				Assert.That(json, Does.Contain(@"\/Date(1234567890000)\/"), "Microsoft date");
				Assert.That(obj.Get<string>("Elapsed", ""), Is.EqualTo("P1DT2H3M4.005S"), "ISO 8601 duration");
				Assert.That(obj["Counts"], Is.InstanceOf<JsonArray>(), "pair-array dictionary");
				Assert.That(obj.ContainsKey("MaybeNull"), Is.True, "explicit null member");
			}
		}

		[Test]
		public void Test_Unprofiled_Container_Over_The_Same_Types_Emits_The_Modern_Output()
		{
			// the dual-container pattern: the SAME type serves both outputs, one container each
			var json = ModernConverters.ProfiledOrder.ToJsonText(MakeSample());
			Log(json);
			var obj = CrystalJson.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Kind"], Is.InstanceOf<JsonString>(), "modern default: enum as string");
				Assert.That(json, Does.Not.Contain("/Date("), "modern default: ISO 8601 date");
				Assert.That(obj["Counts"], Is.InstanceOf<JsonObject>(), "modern default: object-map dictionary");
				Assert.That(obj.ContainsKey("MaybeNull"), Is.False, "modern default: null members omitted");
			}
		}

		[Test]
		public void Test_Explicitly_Passed_Value_Format_Settings_Win_Over_The_Profile()
		{
			// runtime-honorable value-format settings pass through ENTIRELY (no merging): an explicit caller
			// choice is auditable, a merged one is not
			var json = LegacyConverters.ProfiledOrder.ToJsonText(MakeSample(), CrystalJsonSettings.Json);
			var obj = CrystalJson.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Kind"], Is.InstanceOf<JsonString>(), "explicit settings replace the profile entirely");
				Assert.That(json, Does.Not.Contain("/Date("));
			}
		}

		[Test]
		public void Test_Combining_The_Profile_With_A_Naming_Option_Is_A_Build_Error()
		{
			// the DCJS output has no naming policy: [CrystalJsonConverter(DataContractCompat)] next to a camelCase
			// or case-insensitive naming option is a contradiction, rejected at generation time (CJ3-3)
			var compilation = GeneratorProbeHarness.Compile("""
				namespace Probe
				{

					public sealed record ProbeDto
					{
						public int Plain { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat, PropertyNamingPolicy = SnowBank.Data.Json.CrystalJsonKnownNamingPolicy.CamelCase)]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}

				}
				""");
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);

			var rejection = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0013");
			Assert.That(rejection, Is.Not.Null, "the profile + a naming option must be rejected at build time");
			Assert.That(rejection!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(rejection.GetMessage(), Does.Contain("dual-container"), "the remedy steers to the dual-container pattern");
		}

		[Test]
		public void Test_Passed_Naming_Settings_Are_Honored_Not_Silently_Ignored()
		{
			// the no-silent-wrong-output doctrine, in the direction that exists in this design: the emitted names
			// carry both casings, so a camelCase request against a profiled container is HONORED at runtime (the
			// settings replace the profile entirely, names included) - never silently answered with the baked
			// casing. The only true conflict (baking the profile AND a naming option into one container) is a
			// build error, pinned separately.
			var json = LegacyConverters.ProfiledOrder.ToJsonText(MakeSample(), CrystalJsonSettings.Json.CamelCased());
			Log(json);
			var obj = CrystalJson.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("kind"), Is.True, "the camelCase request is honored by the generated names");
				Assert.That(obj.ContainsKey("Kind"), Is.False, "no silent fallback to the declared casing");
				Assert.That(obj["kind"], Is.InstanceOf<JsonString>(), "and the explicit settings replaced the profile's value formats entirely");
			}
		}

	}

}
