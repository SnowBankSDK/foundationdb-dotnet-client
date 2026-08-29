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

	/// <summary>Pins that a registered type the compiler could not resolve is reported cleanly (CJSON0021 on the attribute), instead of surfacing as an internal emitter crash (CJSON0003)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class UnresolvedRegisteredTypeDiagnosticFacts : SimpleTest
	{

		/// <summary>The registered type <c>NotAThing</c> does not exist: its <c>typeof</c> resolves to an error type symbol</summary>
		private const string ProbeSource = """
			namespace Probe
			{
				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(NotAThing))]
				public static partial class BrokenHost
				{
				}
			}
			""";

		[Test]
		public void Test_Unresolved_Registered_Type_Is_Reported_Cleanly()
		{
			var compilation = GeneratorProbeHarness.Compile(ProbeSource);

			var (_, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in generatorDiagnostics) { Log($"generator: {diagnostic}"); }

			// the clean diagnostic points at the unresolved registration, so the author fixes the real compile error...
			Assert.That(
				generatorDiagnostics.Select(static d => d.Id),
				Does.Contain("CJSON0021"),
				"an unresolved registered type must be reported cleanly on the attribute");

			// ... and the emitter must not have crashed reporting an internal exception (CJSON0003) instead
			Assert.That(
				generatorDiagnostics.Select(static d => d.Id),
				Does.Not.Contain("CJSON0003"),
				"the emitter must not crash on an unresolved registered type");
		}

	}

}
