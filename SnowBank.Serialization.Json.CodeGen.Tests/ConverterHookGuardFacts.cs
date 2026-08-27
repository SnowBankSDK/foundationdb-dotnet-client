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

	/// <summary>Pins the fail-closed guard on author-written converter hooks: a method carrying one of the three reserved names is either called, or refused with CJSON0024</summary>
	/// <remarks>A hook that sits unused because of a typo would silently keep the generated member crawl, which is the failure this guard exists to prevent, so an unusable shape is an error and never a fallback.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class ConverterHookGuardFacts : SimpleTest
	{

		/// <summary>Builds a probe whose <c>Cat</c> scope carries the given member declarations</summary>
		private static string Probe(string hookBody) => $$"""
			namespace Probe
			{
				public sealed record Cat
				{
					public string? Name { get; set; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.Cat))]
				public static partial class Host
				{
					public static partial class Cat
					{
			{{hookBody}}
					}
				}
			}
			""";

		private static string[] Run(string hookBody)
		{
			var compilation = GeneratorProbeHarness.Compile(Probe(hookBody));
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic}"); }
			return diagnostics.Select(static d => d.Id).ToArray();
		}

		[Test]
		public void Test_Well_Formed_Hooks_Are_Accepted()
		{
			var ids = Run("""
						public static void Serialize(SnowBank.Data.Json.CrystalJsonWriter writer, Probe.Cat? instance) { }

						public static SnowBank.Data.Json.JsonValue Pack(Probe.Cat? instance, SnowBank.Data.Json.CrystalJsonSettings? settings, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver) => SnowBank.Data.Json.JsonNull.Null;

						public static Probe.Cat Unpack(SnowBank.Data.Json.JsonValue value, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver) => new();
			""");

			Assert.That(ids, Does.Not.Contain("CJSON0024"), "the three documented shapes must be accepted");
		}

		[Test]
		public void Test_Optional_Parameter_Defaults_Are_Ignored()
		{
			// the generator passes every argument explicitly, so a default changes nothing for it
			var ids = Run("""
						public static Probe.Cat Unpack(SnowBank.Data.Json.JsonValue value, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver = default) => new();
			""");

			Assert.That(ids, Does.Not.Contain("CJSON0024"), "a default value on an optional parameter must not change the match");
		}

		[Test]
		public void Test_Non_Nullable_Instance_Parameter_Is_Accepted()
		{
			// both spellings are accepted; the generated null check runs before the call either way
			var ids = Run("""
						public static void Serialize(SnowBank.Data.Json.CrystalJsonWriter writer, Probe.Cat instance) { }
			""");

			Assert.That(ids, Does.Not.Contain("CJSON0024"), "a non-nullable instance parameter must be accepted");
		}

		[Test]
		public void Test_Wrong_Parameter_Order_Is_Refused()
		{
			var ids = Run("""
						public static void Serialize(Probe.Cat? instance, SnowBank.Data.Json.CrystalJsonWriter writer) { }
			""");

			Assert.That(ids, Does.Contain("CJSON0024"), "swapped parameters must be refused, not silently ignored");
		}

		[Test]
		public void Test_Wrong_Return_Type_Is_Refused()
		{
			var ids = Run("""
						public static string Pack(Probe.Cat? instance, SnowBank.Data.Json.CrystalJsonSettings? settings, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver) => "";
			""");

			Assert.That(ids, Does.Contain("CJSON0024"), "a wrong return type must be refused");
		}

		/// <summary>A method one level deeper than the scope is not a hook, and carries none of the reserved names' obligations</summary>
		/// <remarks>This is also as close as a probe can get to a NON-STATIC hook. The scope is declared <c>static partial</c> by the generator, so an author's part must be static too (CS0262 otherwise) and can hold no instance method: the <c>IsStatic</c> arm of the signature check is unreachable from source, and stays as a guard against a future scope shape.</remarks>
		[Test]
		public void Test_A_Method_On_A_Nested_Helper_Is_Not_A_Hook()
		{
			var source = """
				namespace Probe
				{
					public sealed record Cat
					{
						public string? Name { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(Probe.Cat))]
					public static partial class Host
					{
						public static partial class Cat
						{
							public class Helper
							{
								public void Serialize(SnowBank.Data.Json.CrystalJsonWriter writer, Probe.Cat? instance) { }
							}
						}
					}
				}
				""";

			var compilation = GeneratorProbeHarness.Compile(source);
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic}"); }

			// a method that is not a member of the scope itself is not a hook at all, and must not be reported
			Assert.That(diagnostics.Select(static d => d.Id), Does.Not.Contain("CJSON0024"), "only the scope's own members are hooks");
		}

		[Test]
		public void Test_Generic_Hook_Is_Refused()
		{
			var ids = Run("""
						public static Probe.Cat Unpack<T>(SnowBank.Data.Json.JsonValue value, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver) => new();
			""");

			Assert.That(ids, Does.Contain("CJSON0024"), "a generic method cannot be called as a hook");
		}

		[Test]
		public void Test_Ref_Parameter_Is_Refused()
		{
			var ids = Run("""
						public static void Serialize(SnowBank.Data.Json.CrystalJsonWriter writer, ref Probe.Cat? instance) { }
			""");

			Assert.That(ids, Does.Contain("CJSON0024"), "a by-reference parameter cannot be called as a hook");
		}

		[Test]
		public void Test_Refusal_Names_The_Expected_Signature()
		{
			var compilation = GeneratorProbeHarness.Compile(Probe("""
						public static string Pack(Probe.Cat? instance, SnowBank.Data.Json.CrystalJsonSettings? settings, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver) => "";
			"""));

			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			var message = diagnostics.Single(static d => d.Id == "CJSON0024").GetMessage();
			Log($"message: {message}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(message, Does.Contain("static JsonValue Pack(Cat instance, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)"), "the message must name the signature the author has to write");
				Assert.That(message, Does.Contain("Probe.Host.Cat.Pack"), "the message must name the offending method");
			}
		}

		[Test]
		public void Test_A_Type_Without_A_Scope_Declaration_Has_No_Hooks()
		{
			// the generator never sees its own emitted sources, so its own forwarders can never trip the guard
			var compilation = GeneratorProbeHarness.Compile("""
				namespace Probe
				{
					public sealed record Cat
					{
						public string? Name { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(Cat))]
					public static partial class Host
					{
					}
				}
				""");

			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic}"); }

			Assert.That(diagnostics.Select(static d => d.Id), Does.Not.Contain("CJSON0024"), "the generator must not trip its own guard on the forwarders it emits");
		}

	}

}
