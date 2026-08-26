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

namespace SnowBank.Data.Json.Tests
{
	using System.Linq;
	using System.Reflection;

	/// <summary>Runs the Acme DCJS sample zoo (33 synthetic cases mirroring a real DataContract corpus) against
	/// CrystalJson, and pins a per-case expectation: what round-trips, what reads back from the legacy output, and what
	/// is refused. The output in <c>zoo.json</c> was captured from the real DataContractJsonSerializer, and is identical
	/// on .NET Framework 4.7.2 and .NET 10.
	/// <para>The corpus is generated, and each expectation is derived from the reference behavior; an expectation that
	/// disagrees with the committed code is the thing to fix.</para>
	/// <para>The read assertions only check that the bind returns non-null, so a <c>true</c> means the bind did not
	/// throw, not that every value survived.</para></summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[SetInvariantCulture]
	public sealed class CrystalJsonAcmeZooFacts : SimpleTest
	{

		public sealed record ZooCase
		{
			public required string Id { get; init; }

			public required Type RootType { get; init; }

			public required Func<object> Create { get; init; }

			public required string Kind { get; init; }

			public required string DcjsOutput { get; init; }

			public string? DcjsError { get; init; }

			public required string[] LegacyDocuments { get; init; }
		}

		private static Dictionary<string, ZooCase>? CachedCases;

		/// <summary>Discovers the cases by the zoo convention (a type named 'Sample' under Acme.Zoo.Cases.*) and joins
		/// them with the metadata + captured output from the embedded <c>zoo.json</c></summary>
		private static Dictionary<string, ZooCase> LoadZoo()
		{
			if (CachedCases is not null) return CachedCases;

			JsonObject manifest;
			using (var stream = typeof(CrystalJsonAcmeZooFacts).Assembly.GetManifestResourceStream("SnowBank.Core.Tests.Data.JSON.AcmeZoo.zoo.json")!)
			using (var reader = new StreamReader(stream))
			{
				manifest = CrystalJson.Parse(reader.ReadToEnd()).AsObject();
			}
			var metadata = manifest.GetObject("cases");

			var cases = new Dictionary<string, ZooCase>(StringComparer.Ordinal);
			foreach (var sample in typeof(CrystalJsonAcmeZooFacts).Assembly.GetTypes())
			{
				if (sample.Name != "Sample" || sample.Namespace?.StartsWith("Acme.Zoo.Cases.", StringComparison.Ordinal) != true)
				{
					continue;
				}

				var id = (string) sample.GetProperty("Id", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
				var meta = metadata.GetObject(id);
				cases.Add(id, new()
				{
					Id = id,
					RootType = (Type) sample.GetProperty("RootType", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!,
					Create = () => sample.GetMethod("Create", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null)!,
					Kind = meta.Get<string>("kind"),
					DcjsOutput = meta.Get<string?>("expected", null) ?? "",
					DcjsError = meta.Get<string?>("expectedError", null),
					LegacyDocuments = (sample.GetProperty("LegacyDocuments", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as string[]) ?? [ ],
				});
			}

			Assert.That(cases, Has.Count.EqualTo(metadata.Count), "every case in zoo.json must have a matching Sample type, and vice versa");
			return CachedCases = cases;
		}

		public static IEnumerable<string> AllCaseIds => LoadZoo().Keys.OrderBy(x => x, StringComparer.Ordinal);

		#region Derived expectations...

		/// <summary>What CrystalJson is expected to do with a case; derived, not authoritative</summary>
		public sealed record Verdict
		{
			/// <summary>Serializing Create() with default settings succeeds</summary>
			public bool Writes { get; init; } = true;

			/// <summary>CrystalJson can read back its own output into the root type, and re-serializing yields the same text</summary>
			public bool SelfRoundTrips { get; init; } = true;

			/// <summary>CrystalJson binds the captured DCJS output into the root type</summary>
			public bool ReadsDcjsOutput { get; init; } = true;

			/// <summary>CrystalJson binds every legacy at-rest document of the case</summary>
			public bool ReadsLegacyDocuments { get; init; } = true;

			/// <summary>The reason for any 'false' above: the derived expectation or documented divergence this case pins</summary>
			public string? Because { get; init; }
		}

		/// <summary>The per-case derived expectations: everything not listed here is expected to fully work</summary>
		private static readonly Dictionary<string, Verdict> DerivedExpectations = new(StringComparer.Ordinal)
		{
			// [KnownType] gets no support: no discriminator is emitted and "__type" is not consulted; derived types
			// are declared with [JsonDerivedType]. An untagged write emits the runtime type's members, and that
			// document does not read back into an abstract declared type.
			["poly-known-type-abstract"] = new()
			{
				SelfRoundTrips = false, ReadsDcjsOutput = false, ReadsLegacyDocuments = false,
				Because = "no [KnownType] support: abstract members need [JsonPolymorphic]/[JsonDerivedType]",
			},
			["poly-serializer-known-types"] = new()
			{
				SelfRoundTrips = false, ReadsDcjsOutput = false,
				Because = "same stance for knownTypes passed to the serializer constructor",
			},

			// one member, two serializers, two names is a double contract, refused on both paths. The contract name
			// is [DataMember(Name)] when spelled and the member's own name when bare; the remedy is to split the DTO.
			["diagnostic-double-contract"] = new()
			{
				Writes = false, SelfRoundTrips = false, ReadsDcjsOutput = false,
				Because = "conflicting output names are refused by design (split the DTO)",
			},

			// the four callbacks are invoked now, but only in the modern signatures. This corpus case carries the
			// legacy void M(StreamingContext) shape, which every DCJS callsite uses because DCJS requires it, so
			// the whole type is refused until its callbacks are converted (the migration guide's sweep recipe).
			["lifecycle-callbacks"] = new()
			{
				Writes = false, SelfRoundTrips = false, ReadsDcjsOutput = false, ReadsLegacyDocuments = false,
				Because = "the legacy StreamingContext callback signature is refused by design (drop the parameter, or take JsonValue/JsonObject/JsonArray)",
			},
		};

		#endregion

		[Test]
		public void Test_Zoo_Is_Complete()
		{
			var zoo = LoadZoo();
			// 33 since the re-import: `legacy-arraylist-members` was written after the first hand-off
			Assert.That(zoo, Has.Count.EqualTo(33));
			Assert.That(zoo.Values.Count(c => c.Kind == "compatibility"), Is.EqualTo(27));
			Assert.That(zoo.Values.Count(c => c.Kind == "non-equivalence"), Is.EqualTo(2));
			Assert.That(zoo.Values.Count(c => c.Kind == "diagnostic"), Is.EqualTo(4));
			Assert.That(zoo.Values.Sum(c => c.LegacyDocuments.Length), Is.EqualTo(16)); // +1 with the new case
		}

		[TestCaseSource(nameof(AllCaseIds))]
		public void Test_Zoo_Case(string id)
		{
			var zoo = LoadZoo();
			var testCase = zoo[id];
			var verdict = DerivedExpectations.TryGetValue(id, out var expected) ? expected : new Verdict();

			// 1. write direction, default settings
			string? cjOutput = null;
			try
			{
				cjOutput = CrystalJson.Serialize(testCase.Create(), testCase.RootType);
				Log($"cj : {cjOutput}");
				Log($"dcj: {(testCase.DcjsError is null ? testCase.DcjsOutput : $"<threw: {testCase.DcjsError}>")}");
				Assert.That(verdict.Writes, Is.True, $"serialization succeeded but the corpus expected a refusal ({verdict.Because})");
			}
			catch (Exception e) when (e is not AssertionException)
			{
				Log($"cj : <threw: [{e.GetType().Name}] {e.Message}>");
				Assert.That(verdict.Writes, Is.False, $"serialization threw [{e.GetType().Name}] {e.Message}");
			}

			// 2. self round-trip: read back our own output, re-serialize, compare
			if (cjOutput is not null)
			{
				TryStep(
					() =>
					{
						var bound = CrystalJson.Parse(cjOutput).Bind(testCase.RootType)!;
						Assert.That(CrystalJson.Serialize(bound, testCase.RootType), Is.EqualTo(cjOutput), "the self round-trip must be stable");
					},
					verdict.SelfRoundTrips, "self round-trip", verdict.Because);
			}

			// 3. read direction: the captured DCJS output must bind (this is what sits in the application's database)
			if (testCase.DcjsError is null && !string.IsNullOrEmpty(testCase.DcjsOutput) && verdict.Writes)
			{
				TryStep(
					() =>
					{
						var bound = CrystalJson.Parse(testCase.DcjsOutput).Bind(testCase.RootType);
						Assert.That(bound, Is.Not.Null);
					},
					verdict.ReadsDcjsOutput, "read of the DCJS output", verdict.Because);
			}

			// 4. every legacy at-rest document must bind
			foreach (var document in testCase.LegacyDocuments)
			{
				TryStep(
					() =>
					{
						var bound = CrystalJson.Parse(document).Bind(testCase.RootType);
						Assert.That(bound, Is.Not.Null);
					},
					verdict.ReadsLegacyDocuments, $"read of legacy document {document}", verdict.Because);
			}
		}

		[Test]
		public void Test_Zoo_NonPublic_DataMembers_Are_In_The_Output()
		{
			// hybrid rule: [DataMember] on a non-public member of a [DataContract] type serializes automatically,
			// so the CrystalJson output for this case carries the same members and values as the captured DCJS output
			// (the generic runner only proves the binding; this pins the write-side content)
			var testCase = LoadZoo()["member-non-public"];
			var cj = CrystalJson.Parse(CrystalJson.Serialize(testCase.Create(), testCase.RootType));
			Assert.That(cj, IsJson.EqualTo(CrystalJson.Parse(testCase.DcjsOutput)));
		}

		private static void TryStep(Action step, bool expectedToWork, string label, string? because)
		{
			try
			{
				step();
				Assert.That(expectedToWork, Is.True, $"{label} succeeded, but the corpus expected a failure ({because})");
			}
			catch (AssertionException)
			{
				throw;
			}
			catch (Exception e)
			{
				Assert.That(expectedToWork, Is.False, $"{label} failed: [{e.GetType().Name}] {e.Message}");
			}
		}

	}

}
