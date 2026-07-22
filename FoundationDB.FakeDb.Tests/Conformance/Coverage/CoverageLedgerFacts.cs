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
	using System.Reflection;
	using System.Text.RegularExpressions;

	/// <summary>The coverage ledger: integrity of the cell inventory, correctness pins of the classifier, and the coverage report over the corpus and the tagged conformance facts.</summary>
	[TestFixture]
	[Category("Fdb-Coverage")]
	public class CoverageLedgerFacts : SimpleTest
	{

		[Test]
		public void Inventory_Is_Well_Formed()
		{
			var cells = CoverageInventory.Cells;
			Assert.That(cells, Is.Not.Empty);

			var seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (var cell in cells)
			{
				Assert.That(seen.Add(cell.Id), Is.True, $"duplicate cell id '{cell.Id}'");
				Assert.That(Regex.IsMatch(cell.Id, "^[a-z0-9-]+/[a-z0-9-]+$"), Is.True, $"cell id '{cell.Id}' must be group/name in kebab case");
				Assert.That(cell.Description, Is.Not.Null.And.Not.Empty, $"cell '{cell.Id}' needs a description");
			}
		}

		[Test]
		public void Tags_Reference_Known_Cells()
		{
			foreach (var (method, cells) in EnumerateTaggedFacts())
			{
				foreach (var id in cells)
				{
					Assert.That(CoverageInventory.ById.ContainsKey(id), Is.True, $"[CoversCells] on {method.DeclaringType?.Name}.{method.Name} references unknown cell '{id}'");
				}
			}
		}

		[Test]
		public void Classifier_Emits_Only_Known_Cells()
		{
			foreach (var scenario in ScenarioCorpus.All)
			{
				ScenarioGoldens.TryLoad(scenario.Name, out var golden);
				foreach (var id in CoverageClassifier.Classify(scenario, golden))
				{
					Assert.That(CoverageInventory.ById.ContainsKey(id), Is.True, $"scenario '{scenario.Name}' classified into unknown cell '{id}'");
				}
			}
		}

		[TestCase("ryw_get_after_set_clear", "ryw/get-over-set", "ryw/get-over-clear")]
		[TestCase("ryw_get_after_clearrange", "ryw/get-over-clearrange")]
		[TestCase("ryw_get_after_atomic", "ryw/get-over-atomic")]
		[TestCase("ryw_disable_after_read_poisons", "ryw/disable-after-read-poisons-reads", "errors/client-invalid-operation")]
		[TestCase("ryw_snapshot_default_sees_own_writes", "ryw/snapshot-sees-own-writes-default")]
		[TestCase("watch_aba_two_commits", "watches/fire-two-commit-aba")]
		[TestCase("watch_aba_single_commit", "watches/no-fire-single-commit-aba")]
		[TestCase("watch_identical_value_write", "watches/no-fire-identical-value")]
		[TestCase("watch_tx_disposed_without_commit", "watches/settle-on-dispose")]
		[TestCase("watch_metadataversion", "watches/metadata-version-watch")]
		[TestCase("vs_value_stamped", "versionstamps/placement-value", "versionstamps/fate-committed")]
		[TestCase("vs_interleaved_commits", "versionstamps/monotonic-interleaved")]
		[TestCase("vs_stamp_differs_after_conflict_retry", "versionstamps/fate-conflicted", "errors/transaction-invalid-version")]
		[TestCase("vs_multiple_stamped_ops_user_versions", "versionstamps/two-stamps-user-versions")]
		[TestCase("harness_interleave_conflict", "conflicts/read-write-conflict-loses", "errors/not-committed")]
		public void Classifier_Pins(string scenarioName, params string[] expectedCells)
		{
			var scenario = ScenarioCorpus.Get(scenarioName);
			ScenarioGoldens.TryLoad(scenarioName, out var golden);
			var hits = CoverageClassifier.Classify(scenario, golden);
			Assert.That(hits, Is.SupersetOf(expectedCells), $"scenario '{scenarioName}' must at least hit its pinned cells (got: {string.Join(", ", hits.OrderBy(x => x, StringComparer.Ordinal))})");
		}

		[Test]
		public void Selector_Scenarios_Hit_Selector_Cells()
		{
			var scenario = ScenarioCorpus.Get("harness_selectors_ranges");
			ScenarioGoldens.TryLoad(scenario.Name, out var golden);
			var hits = CoverageClassifier.Classify(scenario, golden);
			Assert.That(hits.Any(h => h.StartsWith("selectors/", StringComparison.Ordinal)), Is.True, $"selector scenarios must hit selector cells (got: {string.Join(", ", hits.OrderBy(x => x, StringComparer.Ordinal))})");
		}

		[Test]
		public void Atomic_Matrix_Hits_Absent_And_Present_Cases()
		{
			var scenario = ScenarioCorpus.Get("atomic_matrix_singles");
			ScenarioGoldens.TryLoad(scenario.Name, out var golden);
			var hits = CoverageClassifier.Classify(scenario, golden);
			Assert.That(hits, Does.Contain("atomics/add-absent"));
			Assert.That(hits.Any(h => h.StartsWith("atomics/add-", StringComparison.Ordinal) && h != "atomics/add-absent"), Is.True, $"the singles matrix covers atomics over committed values too (got: {string.Join(", ", hits.OrderBy(x => x, StringComparer.Ordinal))})");
		}

		/// <summary>The ledger itself: derives scenario hits, adds conformance-fact tags, and prints per-group coverage with the uncovered cells.</summary>
		/// <remarks>Reports the gap; it does not fail on it (the FL-24 target governs campaign work, and gating on 100% starts only once the ledger has ever reached it). Structural violations (unknown ids) fail in the dedicated facts above.</remarks>
		[Test]
		public void Ledger_Report()
		{
			var oracleBacked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
			var emulatorOnly = new Dictionary<string, List<string>>(StringComparer.Ordinal);

			foreach (var scenario in ScenarioCorpus.All)
			{
				bool hasGolden = ScenarioGoldens.TryLoad(scenario.Name, out var golden);
				var target = hasGolden ? oracleBacked : emulatorOnly;
				foreach (var id in CoverageClassifier.Classify(scenario, golden))
				{
					if (!target.TryGetValue(id, out var sources)) target[id] = sources = [ ];
					sources.Add(scenario.Name);
				}
			}

			foreach (var (method, cells) in EnumerateTaggedFacts())
			{
				foreach (var id in cells)
				{
					if (!oracleBacked.TryGetValue(id, out var sources)) oracleBacked[id] = sources = [ ];
					sources.Add($"fact:{method.Name}");
				}
			}

			int totalCovered = 0;
			foreach (var group in CoverageInventory.Cells.GroupBy(c => c.Id.Substring(0, c.Id.IndexOf('/'))))
			{
				int covered = group.Count(c => oracleBacked.ContainsKey(c.Id));
				totalCovered += covered;
				Log($"{group.Key,-14} {covered,3}/{group.Count(),-3} covered");
				foreach (var cell in group)
				{
					if (!oracleBacked.ContainsKey(cell.Id))
					{
						Log($"    MISSING {cell.Id}{(emulatorOnly.ContainsKey(cell.Id) ? " (emulator-only hit exists)" : "")}");
					}
				}
			}
			Log($"TOTAL: {totalCovered}/{CoverageInventory.Cells.Count} cells oracle-backed ({100.0 * totalCovered / CoverageInventory.Cells.Count:N1}%)");

			Assert.That(totalCovered, Is.GreaterThan(0), "the corpus plus the tagged facts must cover at least one cell");
		}

		private static IEnumerable<(MethodInfo Method, IReadOnlyList<string> Cells)> EnumerateTaggedFacts()
		{
			foreach (var type in typeof(CoverageLedgerFacts).Assembly.GetTypes())
			{
				foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
				{
					var attr = method.GetCustomAttribute<CoversCellsAttribute>();
					if (attr is not null) yield return (method, attr.Cells);
				}
			}
		}

	}

}
