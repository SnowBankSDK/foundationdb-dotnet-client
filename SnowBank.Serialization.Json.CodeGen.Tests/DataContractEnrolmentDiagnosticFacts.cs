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

	/// <summary>Pins that a <c>[DataContract]</c> type IS enrolled in a generated container, and that the generated converter applies the DataContract contract model rather than the plain-DTO one</summary>
	/// <remarks>
	/// <para>This fixture replaces the interim <c>CJSON0014</c> refusal. That diagnostic existed because generated converters
	/// did not implement the DataContract membership model, so enrolling such a type would have produced an output that silently
	/// differed from the reflection path. The model is implemented now, so the refusal is retired and its absence is pinned here.</para>
	/// <para>The behavioural comparison (generated vs reflection vs the live legacy serializer) lives in
	/// <see cref="DataContractCompatProbeFacts"/>; this fixture only covers what the BUILD reports.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class DataContractEnrolmentDiagnosticFacts : SimpleTest
	{

		private static ImmutableArray<Diagnostic> RunOn(string source)
		{
			var compilation = GeneratorProbeHarness.Compile("namespace Probe\n{\n" + source + "\n}\n");
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

		private const string DataContractDto = """
				[System.Runtime.Serialization.DataContract]
				public sealed record ProbeDto
				{
					[System.Runtime.Serialization.DataMember(Name = "id")]
					public string? Id { get; set; }
				}
			""";

		[Test]
		public void Test_Enrolled_DataContract_Type_Is_Accepted()
		{
			var diagnostics = RunOn(DataContractDto + """

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "enrolling a [DataContract] type is supported and reports nothing");
			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0014"), Is.Empty, "the interim refusal is retired");
		}

		[Test]
		public void Test_Referenced_But_Unenrolled_DataContract_Type_Is_Not_Refused()
		{
			var diagnostics = RunOn(DataContractDto + """

					public sealed record HostDto
					{
						public ProbeDto? Inner { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(HostDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "a referenced [DataContract] type reports nothing either");
		}

		[Test]
		public void Test_Self_Serializable_DataContract_Type_Is_Accepted()
		{
			var diagnostics = RunOn("""
					[SnowBank.Data.Json.CrystalJsonSelfSerializable]
					[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
					public sealed class ProbeEntityAttribute : System.Attribute { }

					[ProbeEntity]
					[System.Runtime.Serialization.DataContract]
					public sealed partial record ProbeDto
					{
						[System.Runtime.Serialization.DataMember(Name = "id")]
						public string? Id { get; set; }
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "self-serialization of a [DataContract] type is supported too");
			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0014"), Is.Empty, "the interim refusal is retired on this route as well");
		}

		[Test]
		public void Test_Enrolled_DataContract_Type_Members_Are_Analyzed()
		{
			// the mirror image of the old behaviour: the type-level refusal used to preempt every member-level
			// diagnostic, so a real conflict inside a [DataContract] type went unreported. Now that the type is
			// enrolled its members are parsed, and the dual-output conflict is caught where it always should have been.
			var diagnostics = RunOn("""
					[System.Runtime.Serialization.DataContract]
					public sealed record ProbeDto
					{
						[System.Runtime.Serialization.DataMember]
						[System.Text.Json.Serialization.JsonIgnore]
						public string? Both { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0008"), Is.Not.Empty, "the member-level conflict is now reported instead of being hidden behind a type-level refusal");
		}

		[Test]
		public void Test_Legacy_StreamingContext_Callback_Is_A_Build_Error()
		{
			var diagnostics = RunOn("""
					[System.Runtime.Serialization.DataContract]
					public sealed record ProbeDto
					{
						[System.Runtime.Serialization.DataMember(Name = "id")]
						public string? Id { get; set; }

						[System.Runtime.Serialization.OnDeserialized]
						private void AfterRead(System.Runtime.Serialization.StreamingContext context) { }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			var refusal = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0015");
			Assert.That(refusal, Is.Not.Null, "the legacy callback signature is refused at build time");
			Assert.That(refusal!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(refusal.GetMessage(), Does.StartWith("Remove the StreamingContext parameter"), "the message leads with the fix");
			Assert.That(refusal.GetMessage(), Does.Contain("ProbeDto.AfterRead"), "and names the offending declaring type and method");
		}

		[Test]
		public void Test_Any_Other_Unusable_Callback_Signature_Is_Also_A_Build_Error()
		{
			// not only the legacy shape: anything generated code cannot invoke is refused, so the generator never
			// stays silent about a callback that the reflection path would reject at contract build
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						public string? Id { get; set; }

						[System.Runtime.Serialization.OnDeserialized]
						private void AfterRead(int unexpected) { }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			var refusal = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0015");
			Assert.That(refusal, Is.Not.Null, "an unusable callback signature is refused at build time");
			Assert.That(refusal!.GetMessage(), Is.EqualTo(string.Format(SnowBank.Data.Json.CrystalJson.Errors.CallbackSignatureNotSupported, "AfterRead")), "and with the same message the reflection path throws");
		}

		[Test]
		public void Test_PrePopulate_Callback_With_An_InitOnly_Or_Required_Member_Is_Refused()
		{
			// without this the consumer gets a compiler error inside generated source they never wrote; the
			// diagnostic points at the code they CAN edit, and the two remedies differ so the messages do
			var initOnly = RunOn("""
					public sealed record ProbeDto
					{
						public string? Id { get; init; }

						[System.Runtime.Serialization.OnDeserializing]
						private void BeforeRead() { }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			var initRefusal = initOnly.SingleOrDefault(static d => d.Id == "CJSON0016");
			Assert.That(initRefusal, Is.Not.Null, "an init-only member cannot be assigned after construction");
			Assert.That(initRefusal!.GetMessage(), Does.StartWith("Change the 'init' accessor"), "the message leads with the remedy for THIS construct");

			var required = RunOn("""
					public sealed record ProbeDto
					{
						public required string Id { get; set; }

						[System.Runtime.Serialization.OnDeserializing]
						private void BeforeRead() { }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			var requiredRefusal = required.SingleOrDefault(static d => d.Id == "CJSON0016");
			Assert.That(requiredRefusal, Is.Not.Null, "a required member can only be set in an object initializer");
			Assert.That(requiredRefusal!.GetMessage(), Does.StartWith("Remove the 'required' modifier"), "and this construct gets its own remedy");
		}

		[Test]
		public void Test_Neither_Construct_Is_A_Problem_On_Its_Own()
		{
			// the conflict is the COMBINATION: an init-only member without a pre-populate callback, and a
			// pre-populate callback without such a member, both stay perfectly legal
			var initOnlyAlone = RunOn("""
					public sealed record ProbeDto
					{
						public string? Id { get; init; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			Assert.That(initOnlyAlone.Where(static d => d.Id == "CJSON0016"), Is.Empty, "an init-only member alone is fine");

			var callbackAlone = RunOn("""
					public sealed record ProbeDto
					{
						public string? Id { get; set; }

						[System.Runtime.Serialization.OnDeserializing]
						private void BeforeRead() { }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			Assert.That(callbackAlone.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "a pre-populate callback alone is fine");
		}

		[Test]
		public void Test_Bad_Boolean_Literal_Type_Is_A_Build_Error()
		{
			// [JsonBooleanLiterals] takes `object` arguments so that null can mean "do not emit", which took the
			// literal's type OFF the compiler. Without this diagnostic the change would be a net safety regression
			// for generated containers, which previously got a compile error from the typed constructors.
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonBooleanLiterals(System.DayOfWeek.Friday, "1")]
						public bool Flag { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			var refusal = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0017");
			Assert.That(refusal, Is.Not.Null, "a literal type with no JSON representation is refused at compile time");
			Assert.That(refusal!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(refusal.GetMessage(), Is.EqualTo(string.Format(SnowBank.Data.Json.CrystalJson.Errors.BooleanLiteralTypeNotSupported, "whenFalse", "DayOfWeek")), "and with the same message the runtime guard throws");
		}

		[Test]
		public void Test_Accepted_Boolean_Literal_Shapes_Are_Not_Refused()
		{
			// all four legal shapes, including the two the object constructor exists for
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonBooleanLiterals("0", "1")]      public bool A { get; set; }
						[SnowBank.Data.Json.JsonBooleanLiterals(0, 1)]          public bool B { get; set; }
						[SnowBank.Data.Json.JsonBooleanLiterals(null, "1")]     public bool C { get; set; }
						[SnowBank.Data.Json.JsonBooleanLiterals(null, true)]    public bool D { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "every legal literal shape compiles clean, with no warning either");
		}

		[Test]
		public void Test_StrictLiterals_Without_A_False_Literal_Warns()
		{
			// deliberately generator-only, and that is not a violation of the both-paths rule. That rule binds a
			// diagnostic that refuses something which would otherwise BEHAVE differently on the two paths, which is
			// CJSON0015. CJSON0016 is generator-only for its own reason: it describes a property of generated code
			// (members assigned as statements after construction), and the reflection path assigns reflectively, so
			// there is nothing there to refuse. This one changes no behaviour at all, it is advice about a pointless
			// combination, so a compile-time nudge is the whole feature.
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonBooleanLiterals(null, true, StrictLiterals = true)]
						public bool Flag { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			var warning = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0018");
			Assert.That(warning, Is.Not.Null, "the contradiction is pointed out where it is written");
			Assert.That(warning!.Severity, Is.EqualTo(DiagnosticSeverity.Warning), "a nudge, suppressible like any analyzer warning: it changes no behaviour");
			Assert.That(warning.GetMessage(), Does.Contain("absence is what carries false"), "the message says WHY it is incoherent, not just that it is");
		}

		[Test]
		public void Test_StrictLiterals_With_A_Real_False_Literal_Does_Not_Warn()
		{
			var diagnostics = RunOn("""
					public sealed record ProbeDto
					{
						[SnowBank.Data.Json.JsonBooleanLiterals("0", "1", StrictLiterals = true)]
						public bool Flag { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0018"), Is.Empty, "strict is meaningful when there IS a false literal to enforce");
		}

		[Test]
		public void Test_The_Two_Paths_Share_One_Refusal_Message()
		{
			// the message exists twice because an analyzer cannot reference SnowBank.Core. Two copies drift; this
			// is the only thing stopping them, and a build error that does not match the documented text is a
			// migration recipe nobody can grep for.
			Assert.That(
				CrystalJsonSourceGenerator.CallbackStreamingContextNotSupportedMessage,
				Is.EqualTo(SnowBank.Data.Json.CrystalJson.Errors.CallbackStreamingContextNotSupported),
				"the generator's message and the reflection path's message must be the same string");
			Assert.That(
				CrystalJsonSourceGenerator.CallbackSignatureNotSupportedMessage,
				Is.EqualTo(SnowBank.Data.Json.CrystalJson.Errors.CallbackSignatureNotSupported),
				"and so must the general unusable-signature message");
			Assert.That(
				CrystalJsonSourceGenerator.BooleanLiteralTypeNotSupportedMessage,
				Is.EqualTo(SnowBank.Data.Json.CrystalJson.Errors.BooleanLiteralTypeNotSupported),
				"and the boolean-literal type guard, which is the one replacing a compiler check");
		}

		[Test]
		public void Test_Profiled_Container_Accepts_DataContract_Too()
		{
			// the DataContractCompat profile governs value FORMATS and the membership model is now independent of it:
			// both a profiled and an unprofiled container serve [DataContract] types
			var diagnostics = RunOn(DataContractDto + """

					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");
			Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "a profiled container enrols [DataContract] types as well");
		}

	}

}
