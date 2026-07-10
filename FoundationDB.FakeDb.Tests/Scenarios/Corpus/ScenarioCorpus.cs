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

namespace FoundationDB.Client.Tests
{
	using System.IO;
	using System.Runtime.CompilerServices;
	using FoundationDB.Client;

	/// <summary>The hand-authored scenario corpus shared by the replay, record and dual-live fixtures.</summary>
	/// <remarks>
	/// <para>These are the phase-2 harness-validation demos, deliberately small; campaign corpora (watches, versionstamps, read-your-writes) are follow-up work.</para>
	/// <para>Authoring notes: scenarios must be deterministic on an otherwise idle backend. Avoid <see cref="ScenarioOp.GetReadVersion"/> after commits — a real cluster's version clock advances with wall time while the emulator's only advances on commits, so those symbols can never match.</para>
	/// </remarks>
	public static class ScenarioCorpus
	{

		/// <summary>All the scenarios of the corpus: the hand-authored entries plus the pinned generator finds (<c>Corpus/Pinned/*.json</c>).</summary>
		public static IReadOnlyList<Scenario> All { get; } =
		[
			HarnessSmoke(),
			HarnessInterleaveConflict(),
			HarnessSelectorsRanges(),
			HarnessWatchSmoke(),
			.. RywCorpus.All(),
			.. WatchesCorpus.All(),
			.. VersionstampsCorpus.All(),
			.. LoadPinned(),
		];

		/// <summary>Returns the scenario with the given name.</summary>
		public static Scenario Get(string name) => All.FirstOrDefault(s => s.Name == name) ?? throw new ArgumentException($"Unknown scenario '{name}'.", nameof(name));

		/// <summary>The pinned-scenarios folder in the source tree (compile-time anchor).</summary>
		private static string PinnedSourceDirectory { get; } = ComputePinnedSourceDirectory();

		private static string ComputePinnedSourceDirectory([CallerFilePath] string sourcePath = "") => Path.Combine(Path.GetDirectoryName(sourcePath)!, "Pinned");

		/// <summary>The pinned-scenarios folder copied next to the test assembly by the build.</summary>
		private static string PinnedOutputDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Scenarios", "Corpus", "Pinned");

		/// <summary>Loads the generator finds pinned as JSON scenarios (a fresh pin in the source tree wins over a stale output copy).</summary>
		private static IEnumerable<Scenario> LoadPinned()
		{
			var byName = new SortedDictionary<string, Scenario>(StringComparer.Ordinal);
			foreach (var dir in (string[]) [ PinnedOutputDirectory, PinnedSourceDirectory ]) // source second: overrides
			{
				if (!Directory.Exists(dir)) continue;
				foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
				{
					var scenario = Scenario.FromJson(CrystalJson.Parse(File.ReadAllText(file)));
					byName[scenario.Name] = scenario;
				}
			}
			return byName.Values;
		}

		/// <summary>Pins a generator find as a permanent regression scenario in the source tree, and returns its path (commit the file).</summary>
		public static string PinScenario(Scenario scenario)
		{
			Directory.CreateDirectory(PinnedSourceDirectory);
			var path = Path.Combine(PinnedSourceDirectory, scenario.Name + ".json");
			File.WriteAllText(path, scenario.ToJson().ToJsonText(CrystalJsonSettings.JsonIndented) + Environment.NewLine);
			return path;
		}

		/// <summary>Test cases for the mode fixtures (one per scenario, displayed by name).</summary>
		public static IEnumerable<TestCaseData> TestCases => All.Select(s => new TestCaseData(s.Name));

		private static Scenario HarnessSmoke()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "hello");
			builder.Get("A", "k1"); // read-your-writes: observes the uncommitted value
			builder.Atomic("A", "counter", Slice.FromFixed64(1), FdbMutationType.Add);
			builder.Commit("A");
			builder.GetCommittedVersion("A");
			builder.Begin("A");
			builder.Get("A", "k1");
			builder.Get("A", "missing");
			builder.Get("A", "counter");
			builder.GetRange("A",
				new ScenarioSelector(Slice.Empty, OrEqual: false, Offset: 1),                  // FirstGreaterOrEqual(<subspace start>)
				new ScenarioSelector(Slice.FromStringAscii("~"), OrEqual: false, Offset: 1));  // FirstGreaterOrEqual(~), past every demo key
			builder.Commit("A"); // committing a read-only transaction succeeds
			builder.Dispose("A");
			return builder.Build("harness_smoke",
				"single actor: set/get with read-your-writes, an atomic add, then re-reads and a full range in a second transaction");
		}

		private static Scenario HarnessInterleaveConflict()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Get("A", "k1");        // A reads k1 (absent)...
			builder.Begin("B");
			builder.Set("B", "k1", "b1");  // ...B writes it...
			builder.Commit("B");           // ...and commits first
			builder.GetCommittedVersion("B");
			builder.Set("A", "k2", "a1");  // A writes something derived from its (now stale) read
			builder.Commit("A");           // read-write conflict: A must lose
			return builder.Build("harness_interleave_conflict",
				"two actors: B commits a write to a key A has read, so A's later commit must fail with a conflict");
		}

		private static Scenario HarnessSelectorsRanges()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "v1");
			builder.Set("A", "k2", "v2");
			builder.Set("A", "k3", "v3");
			builder.Commit("A");
			builder.Begin("A");
			builder.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k2"), OrEqual: false, Offset: 1)); // FirstGreaterOrEqual(k2) -> k2
			builder.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k2"), OrEqual: true, Offset: 1));  // FirstGreaterThan(k2) -> k3
			builder.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k3"), OrEqual: true, Offset: 0));  // LastLessOrEqual(k3) -> k3
			builder.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k1"), OrEqual: false, Offset: 0)); // LastLessThan(k1) -> below the subspace
			builder.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k1"), false, 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), false, 1),
				limit: 2);                 // forward, truncated -> k1, k2 + hasMore
			builder.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k1"), false, 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), false, 1),
				limit: 2, reverse: true);  // reverse, truncated -> k3, k2 + hasMore
			builder.Get("A", "k2", snapshot: true);
			builder.Commit("A");
			return builder.Build("harness_selectors_ranges",
				"key selector resolution (FGE/FGT/LLE/LLT incl. below the subspace), bounded and reverse ranges, snapshot read");
		}

		private static Scenario HarnessWatchSmoke()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "old");
			builder.Commit("A");
			builder.Begin("A");
			int w = builder.Watch("A", "k1");
			builder.Commit("A");
			builder.ExpectPending(w);      // nothing changed the key yet
			builder.Begin("B");
			builder.Set("B", "k1", "new");
			builder.Commit("B");
			builder.ExpectFired(w);        // the other actor's commit fires the watch
			builder.Begin("A");
			builder.Get("A", "k1");        // post-fire read observes the new value
			builder.Commit("A");
			return builder.Build("harness_watch_smoke",
				"harness-machinery validation: a watch stays pending until another actor commits a change, then fires and the new value is readable");
		}

	}

}
