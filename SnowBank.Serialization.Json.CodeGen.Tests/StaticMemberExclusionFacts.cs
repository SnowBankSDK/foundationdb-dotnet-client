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

	/// <summary>Pins that <c>static</c> members are never treated as serialization state: an instance accessor over a static member would not compile (CS0176)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class StaticMemberExclusionFacts : SimpleTest
	{

		/// <summary>A type exposing a <c>public static readonly</c> field and a static property alongside normal instance members</summary>
		private const string ProbeSource = """
			#nullable enable
			namespace Probe
			{
				public sealed record Money
				{
					public int Amount { get; init; }
				}

				public sealed record Wallet
				{
					// static members: not serialization state, and an instance accessor over them does not compile (CS0176)
					public static readonly Money WellKnown = new() { Amount = 100 };
					public static Money Zero { get; } = new() { Amount = 0 };

					// normal instance members
					public string? Owner { get; init; }
					public Money? Balance { get; init; }
				}

				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput]
				[SnowBank.Data.CrystalSerializable(typeof(Wallet))]
				[SnowBank.Data.CrystalSerializable(typeof(Money))]
				public static partial class WalletConverters
				{
				}
			}
			""";

		[Test]
		public void Test_Static_Members_Are_Not_Serialized_And_Generated_Code_Compiles()
		{
			var compilation = GeneratorProbeHarness.Compile(ProbeSource);

			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (outputCompilation, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in generatorDiagnostics) { Log($"generator: {diagnostic}"); }

			var errors = outputCompilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();
			foreach (var diagnostic in errors)
			{
				Log(diagnostic.ToString());
				if (diagnostic.Location.SourceTree is { } tree)
				{
					var span = diagnostic.Location.GetLineSpan();
					var text = tree.GetText();
					if (span.StartLinePosition.Line < text.Lines.Count)
					{
						Log($"    | {text.Lines[span.StartLinePosition.Line].ToString().Trim()}");
					}
				}
			}

			// the load-bearing assertion: a static member emitted as a serialization member produces an instance accessor (CS0176)
			Assert.That(errors.Where(static d => d.Id == "CS0176"), Is.Empty, "a static member must not be emitted with an instance accessor (CS0176)");
			Assert.That(errors, Is.Empty, "the generated container must compile with static members excluded");

			// and the static members must not appear as serialized properties (their names are not part of the output)
			var generated = string.Concat(outputCompilation.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			Assert.That(generated, Does.Not.Contain("\"WellKnown\""), "the static field must not be emitted as a serialized property");
			Assert.That(generated, Does.Not.Contain("\"Zero\""), "the static property must not be emitted as a serialized property");
			// the instance members must still be present
			Assert.That(generated, Does.Contain("\"Owner\""), "instance members must still be serialized");
			Assert.That(generated, Does.Contain("\"Balance\""), "instance members must still be serialized");
		}

	}

}
