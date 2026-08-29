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

	/// <summary>Pins that an registration resolving to a static class is rejected (CJSON0026), instead of generating a converter whose every signature names a type that cannot appear in one</summary>
	/// <remarks>The case that reaches this in practice: a per-type scope is named after the type it serves, so once an author declares their hook part, an unqualified <c>typeof(Cat)</c> inside the container binds to the SCOPE rather than to the serialized type.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class StaticClassRegistrationGuardFacts : SimpleTest
	{

		/// <summary>An author declares a hook part, then registers with the unqualified name, which binds to the scope they just declared</summary>
		private const string ShadowedSource = """
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
					public static partial class Cat
					{
						public static void Serialize(SnowBank.Data.Json.CrystalJsonWriter writer, Probe.Cat? instance) { }
					}
				}
			}
			""";

		/// <summary>The same container, registered with the qualified name, which is the documented remedy</summary>
		private const string QualifiedSource = """
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
						public static void Serialize(SnowBank.Data.Json.CrystalJsonWriter writer, Probe.Cat? instance) { }
					}
				}
			}
			""";

		[Test]
		public void Test_Registration_Shadowed_By_The_Scope_Is_Rejected()
		{
			var compilation = GeneratorProbeHarness.Compile(ShadowedSource);

			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic}"); }

			Assert.That(
				diagnostics.Select(static d => d.Id),
				Does.Contain("CJSON0026"),
				"an registration that resolved to the generated scope must be rejected, not turned into a converter for a static class");
		}

		[Test]
		public void Test_Rejection_Names_The_Qualification_Remedy()
		{
			var compilation = GeneratorProbeHarness.Compile(ShadowedSource);

			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			var message = diagnostics.Single(static d => d.Id == "CJSON0026").GetMessage();
			Log($"message: {message}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(message, Does.Contain("Probe.Host.Cat"), "the message must name what the registration actually resolved to");
				Assert.That(message, Does.Contain("static class"), "the message must state why it cannot be serialized");
				Assert.That(message, Does.Contain("qualified"), "the message must name the remedy, since the one-word cause is invisible in the resulting compiler errors");
			}
		}

		[Test]
		public void Test_The_Rejected_Type_Generates_Nothing()
		{
			var compilation = GeneratorProbeHarness.Compile(ShadowedSource);

			var (containers, _) = GeneratorProbeHarness.RunGeneratorAndCaptureContainers(compilation);

			// the container still parses; it simply has no registered type left, so the emitter never sees the static class
			Assert.That(
				containers.TryGetValue("Host", out var metadata) ? metadata.IncludedTypes.Select(static t => t.Name).ToArray() : [ ],
				Does.Not.Contain("Cat"),
				"a rejected registration must not reach the emitter");
		}

		[Test]
		public void Test_A_Qualified_Registration_Is_Accepted()
		{
			var compilation = GeneratorProbeHarness.Compile(QualifiedSource);

			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic}"); }

			Assert.That(
				diagnostics.Select(static d => d.Id),
				Does.Not.Contain("CJSON0026"),
				"the documented remedy must produce no diagnostic");
		}

	}

}
